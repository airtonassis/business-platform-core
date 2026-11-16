using System.Globalization;
using System.Text;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Server-loaded membership facts; never a client authorization assertion.</summary>
public sealed record TenantMembership(Guid MembershipId, Guid AccountId, Guid OrganizationId,
    string Role, string Status, bool AccountActive, long Version);

/// <summary>Explicit C08 commands. No arbitrary role/status patch or reactivation.</summary>
public enum MembershipAction {
    /// <summary>Read one authorized membership.</summary>
    Read,
    /// <summary>Owner promotes another active Member to Admin.</summary>
    Promote,
    /// <summary>Owner demotes another active Admin to Member.</summary>
    Demote,
    /// <summary>Joint Account and Membership revocation.</summary>
    Revoke
}

/// <summary>Same tenant, active actors and immutable Owner, including multiple Owners.</summary>
public static class TenantMembershipPolicy
{
    /// <summary>Evaluates authoritative actor and target facts; SQL repeats this after locks.</summary>
    public static bool Allows(EnterpriseContext actor, TenantMembership target, MembershipAction action)
    {
        if (actor.OrganizationId != target.OrganizationId || actor.Role is not ("Owner" or "Admin")) return false;
        if (action == MembershipAction.Read) return true;
        if (actor.AccountId == target.AccountId || !target.AccountActive || target.Status != "Active") return false;
        return action switch {
            MembershipAction.Promote => actor.Role == "Owner" && target.Role == "Member",
            MembershipAction.Demote => actor.Role == "Owner" && target.Role == "Admin",
            MembershipAction.Revoke => target.Role == "Member" || actor.Role == "Owner" && target.Role == "Admin",
            _ => false
        };
    }
}

/// <summary>Strong canonical version bound to organization and membership, not a collection ETag.</summary>
public static class MembershipVersion
{
    /// <summary>Opaque quoted transport encoding; not a grant of authority.</summary>
    public static string Encode(TenantMembership value) => "\"" + Convert.ToBase64String(Encoding.UTF8.GetBytes(
        $"m1|{value.OrganizationId:D}|{value.MembershipId:D}|{value.Version.ToString(CultureInfo.InvariantCulture)}")).TrimEnd('=').Replace('+','-').Replace('/','_') + "\"";
    /// <summary>Rejects wildcards, lists, weak tags, noncanonical encodings and tags for other items.</summary>
    public static bool TryDecode(string value, TenantMembership target, out long version)
    {
        version = 0;
        if (value.Length is < 3 or > 200 || value[0] != '"' || value[^1] != '"') return false;
        try {
            var text=value[1..^1].Replace('-','+').Replace('_','/');
            var parts=Encoding.UTF8.GetString(Convert.FromBase64String(text.PadRight((text.Length+3)/4*4,'='))).Split('|');
            return parts.Length==4 && parts[0]=="m1" && parts[1]==target.OrganizationId.ToString("D") &&
                parts[2]==target.MembershipId.ToString("D") && long.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out version) &&
                version>0 && Encode(target with { Version=version })==value;
        } catch (FormatException) { return false; }
    }
}

/// <summary>Restricted transactional port: revalidate context and C08 in the database.</summary>
public interface ITenantMembershipStore
{
    /// <summary>Read or mutate one member; operation identifier is server-generated.</summary>
    Task<Result<TenantMembership>> ExecuteAsync(OidcSubject subject, Guid organization, Guid membership,
        MembershipAction action, long expectedVersion, Guid operation, CancellationToken token);
}

/// <summary>Membership commands callable equally from HTTP and other trusted adapters.</summary>
public sealed class ManageTenantMembership(ResolveEnterpriseContext resolve, IAuthenticatedSubject authentication, ITenantMembershipStore store)
{
    /// <summary>Authorization precedes version diagnostics, then SQL revalidates atomically.</summary>
    public async Task<Result<TenantMembership>> ExecuteAsync(Guid organization, Guid membership, MembershipAction action,
        string? ifMatch=null, CancellationToken token=default)
    {
        var resolved=await resolve.ExecuteAsync(organization,token);
        if(resolved.IsFailure) return Result.Failure<TenantMembership>(resolved.Error);
        if(resolved.Value.State==EnterpriseContextState.Inconsistent) return Fail("Context.Inconsistent");
        if(resolved.Value.State!=EnterpriseContextState.Allowed) return Fail("Resource.NotFound");
        var actor=resolved.Value.Context!;
        if(actor.Role is not ("Owner" or "Admin") || !Enum.IsDefined(action) ||
            (action is MembershipAction.Promote or MembershipAction.Demote && actor.Role!="Owner")) return Fail("Access.Forbidden");
        var subject=authentication.GetSubject();
        if(subject is null) return Fail("Authentication.Required");
        var loaded=await store.ExecuteAsync(subject,organization,membership,MembershipAction.Read,0,Guid.Empty,token);
        if(loaded.IsFailure) return loaded;
        if(!TenantMembershipPolicy.Allows(actor,loaded.Value,action)) return Fail("Access.Forbidden");
        if(action==MembershipAction.Read) return loaded;
        if(ifMatch is null) return Fail("Membership.VersionRequired");
        if(!MembershipVersion.TryDecode(ifMatch,loaded.Value,out var version)) return Fail("Membership.InvalidVersion");
        return await store.ExecuteAsync(subject,organization,membership,action,version,Guid.NewGuid(),token);
    }
    private static Result<TenantMembership> Fail(string code)=>Result.Failure<TenantMembership>(new(code,"Membership operation cannot be completed."));
}
