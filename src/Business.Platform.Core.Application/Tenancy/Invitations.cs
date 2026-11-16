using System.Globalization;
using System.Text;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Invitation metadata with no issuer profile, reference hash or secret hash.</summary>
public sealed record InvitationDetails(Guid InvitationId, Guid OrganizationId, Guid RecipientUserId, string Role,
    string State, long Version, string OrganizationName, DateTimeOffset CreatedAtUtc, DateTimeOffset ExpiresAtUtc, Guid? AccountId, Guid? MembershipId);
/// <summary>Transactional outcome; replay never recovers a secret.</summary>
public sealed record InvitationOutcome(InvitationDetails Invitation, bool Replayed);
/// <summary>Only the first successful issue/replace response contains the new code.</summary>
public sealed record IssuedInvitation(InvitationDetails Invitation, string? Code, bool Replayed, bool SecretUnavailable)
{
    /// <inheritdoc />
    public override string ToString()=>"IssuedInvitation [redacted]";
}
/// <summary>Stable keyset metadata page.</summary>
public sealed record InvitationPage(IReadOnlyList<InvitationDetails> Items,string? NextCursor);
/// <summary>Internal typed port request. Hashes come from server-side codec, never a boolean proof.</summary>
public sealed record InvitationCommand(Guid OrganizationId,string Action,Guid InvitationId=default,Guid RecipientUserId=default,
    byte[]? ReferenceHash=null,byte[]? SecretHash=null,long Version=0,Guid Key=default,int Limit=25,TenantAuditPosition? Before=null,string? StateFilter=null)
{
    /// <inheritdoc />
    public override string ToString()=>"InvitationCommand [redacted]";
}
/// <summary>Two separate SQL surfaces: authorized Tenant management and recipient SELF.</summary>
public interface IInvitationStore
{
    /// <summary>Management metadata or explicit command, revalidated transactionally.</summary>
    Task<Result<InvitationOutcome>> ManageAsync(OidcSubject subject,InvitationCommand command,CancellationToken token);
    /// <summary>Bounded same-Tenant list.</summary>
    Task<Result<IReadOnlyList<InvitationDetails>>> ListAsync(OidcSubject subject,InvitationCommand command,CancellationToken token);
    /// <summary>Secret and authenticated recipient required; no organization parameter.</summary>
    Task<Result<InvitationOutcome>> RecipientAsync(OidcSubject subject,Guid invitationId,byte[] hash,bool accept,long version,CancellationToken token);
}
/// <summary>Strong invitation-specific ETag, bound to organization and item.</summary>
public static class InvitationVersion
{
    /// <summary>Canonical typed individual version.</summary>
    public static string Encode(InvitationDetails value)=>"\""+Convert.ToBase64String(Encoding.UTF8.GetBytes($"i1|{value.OrganizationId:D}|{value.InvitationId:D}|{value.Version.ToString(CultureInfo.InvariantCulture)}")).TrimEnd('=').Replace('+','-').Replace('/','_')+"\"";
    /// <summary>No weak/list/wildcard/cross-item preconditions.</summary>
    public static bool TryDecode(string text,InvitationDetails target,out long version)
    {
        version=0;if(text.Length is <3 or >200 || text[0]!='"' || text[^1]!='"')return false;
        try {
            var s=text[1..^1].Replace('-','+').Replace('_','/');var p=Encoding.UTF8.GetString(Convert.FromBase64String(s.PadRight((s.Length+3)/4*4,'='))).Split('|');
            return p.Length==4 && p[0]=="i1" && p[1]==target.OrganizationId.ToString("D") && p[2]==target.InvitationId.ToString("D") && long.TryParse(p[3],NumberStyles.None,CultureInfo.InvariantCulture,out version) && version>0 && Encode(target with{Version=version})==text;
        } catch(FormatException){return false;}
    }
}
/// <summary>C04/C08 invitation use cases. No invitation lifecycle is inferred from claims.</summary>
public sealed class Invitations(ResolveEnterpriseContext resolve,IAuthenticatedSubject authentication,IInvitationStore store)
{
    private async Task<Result<OidcSubject>> Manager(Guid org,CancellationToken token)
    {
        var context=await resolve.ExecuteAsync(org,token);
        if(context.IsFailure)return Result.Failure<OidcSubject>(context.Error);
        if(context.Value.State==EnterpriseContextState.Inconsistent)return Result.Failure<OidcSubject>(Error("Context.Inconsistent"));
        if(context.Value.State!=EnterpriseContextState.Allowed)return Result.Failure<OidcSubject>(Error("Invitation.Unavailable"));
        if(context.Value.Context!.Role is not ("Owner" or "Admin"))return Result.Failure<OidcSubject>(Error("Access.Forbidden"));
        var subject=authentication.GetSubject();return subject is null?Result.Failure<OidcSubject>(Error("Authentication.Required")):Result.Success(subject);
    }
    /// <summary>Issue a Member invitation, using approved C02 proof and a canonical operation key.</summary>
    public async Task<Result<IssuedInvitation>> IssueAsync(Guid org,Guid recipient,string reference,string? idempotencyKey,CancellationToken token=default)
    {
        var actor=await Manager(org,token);if(actor.IsFailure)return Result.Failure<IssuedInvitation>(actor.Error);
        if(recipient==Guid.Empty || !OwnerReferenceCodec.TryHash(reference,out var proof))return Result.Failure<IssuedInvitation>(Error("Recipient.Unavailable"));
        if(!Key(idempotencyKey,out var key))return Result.Failure<IssuedInvitation>(Error("Invitation.InvalidRequest"));
        var code=OwnerReferenceCodec.Generate();OwnerReferenceCodec.TryHash(code,out var hash);
        var result=await store.ManageAsync(actor.Value,new(org,"Issue",RecipientUserId:recipient,ReferenceHash:proof,SecretHash:hash,Key:key),token);
        if(result.IsFailure && result.Error.Code=="Invitation.Unavailable")
            return Result.Failure<IssuedInvitation>(Error("Recipient.Unavailable"));
        return Issued(result,code);
    }
    /// <summary>Same-Tenant manager item, no code recovery.</summary>
    public async Task<Result<InvitationOutcome>> GetAsync(Guid org,Guid id,CancellationToken token=default)
    {
        var actor=await Manager(org,token);return actor.IsFailure?Result.Failure<InvitationOutcome>(actor.Error):await store.ManageAsync(actor.Value,new(org,"Get",id),token);
    }
    /// <summary>Ordered metadata page with an exact bounded cursor.</summary>
    public async Task<Result<InvitationPage>> ListAsync(Guid org,int limit=25,string? cursor=null,string? state=null,CancellationToken token=default)
    {
        var actor=await Manager(org,token);if(actor.IsFailure)return Result.Failure<InvitationPage>(actor.Error);
        if(state is not (null or "Pending" or "Accepted" or "Expired" or "Cancelled" or "Replaced") || limit is <1 or >100 || !TenantAuditPosition.TryDecode(cursor,out var position))return Result.Failure<InvitationPage>(Error("Invitation.InvalidRequest"));
        var result=await store.ListAsync(actor.Value,new(org,"List",Limit:limit,Before:position,StateFilter:state),token);
        if(result.IsFailure)return Result.Failure<InvitationPage>(result.Error);
        var items=result.Value.Take(limit).ToArray();return Result.Success(new InvitationPage(items,result.Value.Count>limit?new TenantAuditPosition(items[^1].CreatedAtUtc,items[^1].InvitationId).Encode():null));
    }
    /// <summary>Cancel/replace an authorized item; SQL handles replay before stale-state checks.</summary>
    public async Task<Result<IssuedInvitation>> ChangeAsync(Guid org,Guid id,bool replace,string? ifMatch,string? idempotencyKey=null,CancellationToken token=default)
    {
        var actor=await Manager(org,token);if(actor.IsFailure)return Result.Failure<IssuedInvitation>(actor.Error);
        var item=await store.ManageAsync(actor.Value,new(org,"Get",id),token);if(item.IsFailure)return Result.Failure<IssuedInvitation>(item.Error);
        if(ifMatch is null)return Result.Failure<IssuedInvitation>(Error("Invitation.VersionRequired"));
        if(!InvitationVersion.TryDecode(ifMatch,item.Value.Invitation,out var version))return Result.Failure<IssuedInvitation>(Error("Invitation.InvalidRequest"));
        var key=Guid.NewGuid();if(replace && !Key(idempotencyKey,out key))return Result.Failure<IssuedInvitation>(Error("Invitation.InvalidRequest"));
        var code=replace?OwnerReferenceCodec.Generate():null;byte[]? hash=null;if(code is not null)OwnerReferenceCodec.TryHash(code,out hash);
        var result=await store.ManageAsync(actor.Value,new(org,replace?"Replace":"Cancel",id,SecretHash:hash,Version:version,Key:key),token);
        if(result.IsFailure && result.Error.Code=="Invitation.Conflict") {
            var current=await store.ManageAsync(actor.Value,new(org,"Get",id),token);
            if(current.IsFailure)return Result.Failure<IssuedInvitation>(current.Error);
            return Result.Failure<IssuedInvitation>(Error(StateConflict(current.Value.Invitation.State)));
        }
        return Issued(result,code);
    }
    /// <summary>Authenticated recipient plus code; no Tenant role needed for a never-linked identity.</summary>
    public async Task<Result<InvitationOutcome>> RecipientAsync(Guid id,string code,bool accept=false,string? ifMatch=null,CancellationToken token=default)
    {
        var subject=authentication.GetSubject();if(subject is null)return Result.Failure<InvitationOutcome>(Error("Authentication.Required"));
        if(!OwnerReferenceCodec.TryHash(code,out var hash))return Result.Failure<InvitationOutcome>(Error("Invitation.Unavailable"));
        var preview=await store.RecipientAsync(subject,id,hash,false,0,token);
        if(preview.IsFailure || !accept)return preview;
        long version=0;
        if(preview.Value.Invitation.State!="Accepted") {
            if(ifMatch is null)return Result.Failure<InvitationOutcome>(Error("Invitation.VersionRequired"));
            if(!InvitationVersion.TryDecode(ifMatch,preview.Value.Invitation,out version))return Result.Failure<InvitationOutcome>(Error("Invitation.InvalidRequest"));
        }
        var accepted=await store.RecipientAsync(subject,id,hash,true,version,token);
        if(accepted.IsFailure && accepted.Error.Code=="Invitation.Conflict") {
            // A bounded read after a confirmed SQL conflict refreshes state and proof;
            // it never retries the mutation or infers success after transport failure.
            var current=await store.RecipientAsync(subject,id,hash,false,0,token);
            if(current.IsFailure)return current;
            return Result.Failure<InvitationOutcome>(Error(StateConflict(current.Value.Invitation.State)));
        }
        return accepted;
    }
    private static string StateConflict(string state)=>state switch {
        "Expired"=>"Invitation.Expired","Cancelled"=>"Invitation.Cancelled","Replaced"=>"Invitation.Replaced",
        "Accepted"=>"Invitation.AlreadyAccepted",_=>"Invitation.Conflict"
    };
    private static bool Key(string? value,out Guid key)=>Guid.TryParseExact(value,"D",out key)&&key!=Guid.Empty;
    private static Result<IssuedInvitation> Issued(Result<InvitationOutcome> result,string? code)=>result.IsFailure?Result.Failure<IssuedInvitation>(result.Error):Result.Success(new IssuedInvitation(result.Value.Invitation,result.Value.Replayed?null:code,result.Value.Replayed,result.Value.Replayed && code is not null));
    private static Error Error(string code)=>new(code,"Invitation operation cannot be completed.");
}
