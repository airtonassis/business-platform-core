using System.Globalization;
using System.Text;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Sandbox resource with immutable tenant and persisted aggregate version.</summary>
public sealed record DemoRecord(Guid Id,Guid OrganizationId,string Title,long Version);
/// <summary>Bounded deterministic page, never a collection mutation version.</summary>
public sealed record DemoRecordPage(DemoRecord[] Items,bool HasMore);
/// <summary>Create receipt returns the original representation without a second mutation.</summary>
public sealed record DemoRecordCreated(DemoRecord Item,bool Replayed);
/// <summary>Approved DemoRecord input and role rules.</summary>
public static class DemoRecordRules
{
    /// <summary>Only authoritative Owner/Admin roles write.</summary>
    public static bool CanWrite(string role)=>role is "Owner" or "Admin";
    /// <summary>Canonical title is trimmed; limit counts Unicode scalar values as PostgreSQL does.</summary>
    public static bool ValidTitle(string? title)=>title is not null && title.Trim().EnumerateRunes().Count() is >=1 and <=200;
}
/// <summary>Strong opaque tag bound to this resource and tenant.</summary>
public static class DemoRecordVersion
{
    /// <summary>Canonical version encoding.</summary>
    public static string Encode(DemoRecord item)=>"\""+Convert.ToBase64String(Encoding.UTF8.GetBytes($"d1|{item.OrganizationId:D}|{item.Id:D}|{item.Version.ToString(CultureInfo.InvariantCulture)}")).TrimEnd('=').Replace('+','-').Replace('/','_')+"\"";
    /// <summary>Rejects foreign, weak, list and wildcard tags.</summary>
    public static bool TryDecode(string value,DemoRecord target,out long version)
    {
        version=0;
        if(value.Length is <3 or >200 || value[0]!='"' || value[^1]!='"') return false;
        try {
            var encoded=value[1..^1].Replace('-','+').Replace('_','/');
            var parts=Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'='))).Split('|');
            return parts.Length==4 && parts[0]=="d1" && parts[1]==target.OrganizationId.ToString("D") && parts[2]==target.Id.ToString("D") &&
                long.TryParse(parts[3],NumberStyles.None,CultureInfo.InvariantCulture,out version) && version>0 && Encode(target with{Version=version})==value;
        } catch(FormatException) { return false; }
    }
}
/// <summary>Restricted SQL ports; identity comes from authenticated server context.</summary>
public interface IDemoRecordStore
{
    /// <summary>Read a same-tenant item.</summary>
    Task<Result<DemoRecord>> ReadAsync(OidcSubject subject,Guid org,Guid id,CancellationToken token);
    /// <summary>List ascending immutable IDs with a bounded offset.</summary>
    Task<Result<DemoRecordPage>> ListAsync(OidcSubject subject,Guid org,int limit,int offset,CancellationToken token);
    /// <summary>Atomically create item, event and 48-hour receipt.</summary>
    Task<Result<DemoRecordCreated>> CreateAsync(OidcSubject subject,Guid org,string title,Guid key,CancellationToken token);
    /// <summary>Atomically update title/version and event after reauthorization.</summary>
    Task<Result<DemoRecord>> UpdateAsync(OidcSubject subject,Guid org,Guid id,string title,long version,CancellationToken token);
}
/// <summary>Application authorization applies equally outside HTTP; SQL repeats it under locks.</summary>
public sealed class ManageDemoRecords(ResolveEnterpriseContext resolve,IAuthenticatedSubject authentication,IDemoRecordStore store)
{
    private async Task<Result<OidcSubject>> Authorize(Guid org,bool write,CancellationToken token)
    {
        var ctx=await resolve.ExecuteAsync(org,token);
        if(ctx.IsFailure) return Result.Failure<OidcSubject>(ctx.Error);
        if(ctx.Value.State==EnterpriseContextState.Inconsistent) return Fail<OidcSubject>("Context.Inconsistent");
        if(ctx.Value.State!=EnterpriseContextState.Allowed) return Fail<OidcSubject>("Resource.NotFound");
        if(write && !DemoRecordRules.CanWrite(ctx.Value.Context!.Role)) return Fail<OidcSubject>("Access.Forbidden");
        var subject=authentication.GetSubject();
        return subject is null?Fail<OidcSubject>("Authentication.Required"):Result.Success(subject);
    }
    /// <summary>Read after server-side authorization.</summary>
    public async Task<Result<DemoRecord>> ReadAsync(Guid org,Guid id,CancellationToken token=default)
    {
        var actor=await Authorize(org,false,token);
        return actor.IsFailure?Result.Failure<DemoRecord>(actor.Error):await store.ReadAsync(actor.Value,org,id,token);
    }
    /// <summary>Limit defaults to 25, maximum 100; offsets stop at 10000 before I/O.</summary>
    public async Task<Result<DemoRecordPage>> ListAsync(Guid org,int limit=25,int offset=0,CancellationToken token=default)
    {
        var actor=await Authorize(org,false,token);
        if(actor.IsFailure) return Result.Failure<DemoRecordPage>(actor.Error);
        if(limit is <1 or >100 || offset is <0 or >10000) return Fail<DemoRecordPage>("DemoRecord.InvalidPage");
        return await store.ListAsync(actor.Value,org,limit,offset,token);
    }
    /// <summary>Canonical payload and explicit scoped idempotency key.</summary>
    public async Task<Result<DemoRecordCreated>> CreateAsync(Guid org,string? title,Guid key,CancellationToken token=default)
    {
        var actor=await Authorize(org,true,token);
        if(actor.IsFailure) return Result.Failure<DemoRecordCreated>(actor.Error);
        if(!DemoRecordRules.ValidTitle(title) || key==Guid.Empty) return Fail<DemoRecordCreated>("DemoRecord.InvalidInput");
        return await store.CreateAsync(actor.Value,org,title!.Trim(),key,token);
    }
    /// <summary>Authorize before version diagnostics; no automatic overwrite/retry.</summary>
    public async Task<Result<DemoRecord>> UpdateAsync(Guid org,Guid id,string? title,string? ifMatch,CancellationToken token=default)
    {
        var actor=await Authorize(org,true,token);
        if(actor.IsFailure) return Result.Failure<DemoRecord>(actor.Error);
        var loaded=await store.ReadAsync(actor.Value,org,id,token);
        if(loaded.IsFailure) return loaded;
        if(ifMatch is null) return Fail<DemoRecord>("DemoRecord.VersionRequired");
        if(!DemoRecordVersion.TryDecode(ifMatch,loaded.Value,out var version)) return Fail<DemoRecord>("DemoRecord.InvalidVersion");
        if(!DemoRecordRules.ValidTitle(title)) return Fail<DemoRecord>("DemoRecord.InvalidInput");
        return await store.UpdateAsync(actor.Value,org,id,title!.Trim(),version,token);
    }
    private static Result<T> Fail<T>(string code)=>Result.Failure<T>(new(code,"DemoRecord operation cannot be completed."));
}
