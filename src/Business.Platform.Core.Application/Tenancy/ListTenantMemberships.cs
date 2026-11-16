using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Bounded membership metadata page, no identity profile.</summary>
public sealed record TenantMembershipPage(TenantMembership[] Items,bool HasMore);
/// <summary>Separate read-only Tenant listing port.</summary>
public interface ITenantMembershipList
{
    /// <summary>Authoritative same-tenant Owner/Admin metadata query.</summary>
    Task<Result<TenantMembershipPage>> ReadAsync(OidcSubject subject,Guid organization,int limit,int offset,CancellationToken token);
}
/// <summary>Read-only list authorization; never trusts role claims.</summary>
public sealed class ListTenantMemberships(ResolveEnterpriseContext resolve,IAuthenticatedSubject authentication,ITenantMembershipList store)
{
    /// <summary>Defaults 25; maximum 100 and offset 10000.</summary>
    public async Task<Result<TenantMembershipPage>> ExecuteAsync(Guid org,int limit=25,int offset=0,CancellationToken token=default)
    {
        var context=await resolve.ExecuteAsync(org,token);
        if(context.IsFailure)return Result.Failure<TenantMembershipPage>(context.Error);
        if(context.Value.State!=EnterpriseContextState.Allowed)return Fail("Resource.NotFound");
        if(context.Value.Context!.Role is not ("Owner" or "Admin"))return Fail("Access.Forbidden");
        if(limit is <1 or >100 || offset is <0 or >10000)return Fail("Membership.InvalidPage");
        var subject=authentication.GetSubject();if(subject is null)return Fail("Authentication.Required");
        return await store.ReadAsync(subject,org,limit,offset,token);
    }
    private static Result<TenantMembershipPage> Fail(string code)=>Result.Failure<TenantMembershipPage>(new(code,"Membership list unavailable."));
}
