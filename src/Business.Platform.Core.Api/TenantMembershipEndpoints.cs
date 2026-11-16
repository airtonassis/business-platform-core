using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

namespace Business.Platform.Core.Api;

public static class TenantMembershipEndpoints
{
    public static void AddServices(IServiceCollection services,IConfiguration configuration)
    {
        services.AddScoped<ManageTenantMembership>();
        services.AddScoped<ListTenantMemberships>();
        services.AddSingleton<ITenantMembershipList,PostgresTenantMembershipList>();
        services.AddSingleton<ITenantMembershipStore,PostgresTenantMembershipStore>();
        services.AddSingleton(_=> {
            if(!configuration.GetSection("TenantDatabase").Exists()) return new TenantMembershipDataSource(null);
            var options=new PostgresOptions();configuration.GetSection("TenantDatabase").Bind(options);
            foreach(var section in new[]{"Database","PlatformDatabase","AuditDatabase"})
                if(options.Username==configuration[section+":Username"]) throw new InvalidOperationException("Dedicated Tenant database configuration required.");
            return new TenantMembershipDataSource(NpgsqlDataSource.Create(options.ConnectionString()));
        });
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{orgId:guid}/memberships",async(Guid orgId,ListTenantMemberships useCase,HttpContext http,CancellationToken token)=>{
            if(http.Items.ContainsKey("IdentityBootstrapUnavailable"))return Results.Problem(statusCode:503,title:"Dependency unavailable.");
            var limit=!http.Request.Query.ContainsKey("limit")?25:int.TryParse(http.Request.Query["limit"],out var size)?size:0;
            var offset=!http.Request.Query.ContainsKey("offset")?0:int.TryParse(http.Request.Query["offset"],out var skip)?skip:-1;
            var result=await useCase.ExecuteAsync(orgId,limit,offset,token);
            if(result.IsFailure)return Results.Problem(statusCode:result.Error.Code switch{"Access.Forbidden"=>403,"Resource.NotFound"=>404,"Membership.InvalidPage"=>400,"Authentication.Required"=>401,_=>503},title:"Membership list unavailable.",extensions:new Dictionary<string,object?>{{"code",result.Error.Code}});
            return Results.Ok(new{items=result.Value.Items.Select(member=>new{member.MembershipId,member.AccountId,member.OrganizationId,member.Role,member.Status,resourceVersion=MembershipVersion.Encode(member)}),result.Value.HasMore});
        }).RequireAuthorization();
        async Task<IResult> Execute(Guid orgId,Guid membershipId,MembershipAction action,ManageTenantMembership useCase,HttpContext http,CancellationToken token) {
            if(http.Items.ContainsKey("IdentityBootstrapUnavailable")) return Results.Problem(statusCode:503,title:"Dependency unavailable.");
            var result=await useCase.ExecuteAsync(orgId,membershipId,action,http.Request.Headers.IfMatch.Count==0 ? null : http.Request.Headers.IfMatch.ToString(),token);
            if(result.IsFailure) return Results.Problem(statusCode:result.Error.Code switch {
                "Authentication.Required"=>401,"Resource.NotFound"=>404,"Access.Forbidden"=>403,
                "Membership.VersionRequired"=>428,"Membership.InvalidVersion"=>400,"Membership.VersionConflict"=>412,
                "Context.Inconsistent"=>500,_=>503
            },title:"Membership operation cannot be completed.",extensions:new Dictionary<string,object?>{{"code",result.Error.Code}});
            var member=result.Value;http.Response.Headers.ETag=MembershipVersion.Encode(member);
            return Results.Ok(new { member.MembershipId,member.AccountId,member.OrganizationId,member.Role,member.Status,resourceVersion=MembershipVersion.Encode(member) });
        }
        app.MapGet("/api/v1/organizations/{orgId:guid}/memberships/{membershipId:guid}",
            (Guid orgId,Guid membershipId,ManageTenantMembership useCase,HttpContext http,CancellationToken token)=>Execute(orgId,membershipId,MembershipAction.Read,useCase,http,token)).RequireAuthorization();
        foreach(var action in new[]{MembershipAction.Promote,MembershipAction.Demote,MembershipAction.Revoke}) {
            var command=action;
            app.MapPost("/api/v1/organizations/{orgId:guid}/memberships/{membershipId:guid}/"+action.ToString().ToLowerInvariant(),
                (Guid orgId,Guid membershipId,ManageTenantMembership useCase,HttpContext http,CancellationToken token)=>Execute(orgId,membershipId,command,useCase,http,token)).RequireAuthorization();
        }
    }
}
