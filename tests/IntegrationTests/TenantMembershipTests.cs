using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Business.Platform.Core.Api;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class TenantMembershipTests
{
    private static readonly Guid Org=Guid.NewGuid(), Account=Guid.NewGuid(), Target=Guid.NewGuid(), Identity=Guid.NewGuid(), Member=Guid.NewGuid();
    private static TenantMembership Membership(string role="Member")=>new(Member,Target,Org,role,"Active",true,1);
    [Theory]
    [InlineData("Owner","Member",MembershipAction.Promote,true)]
    [InlineData("Owner","Admin",MembershipAction.Demote,true)]
    [InlineData("Owner","Admin",MembershipAction.Revoke,true)]
    [InlineData("Owner","Member",MembershipAction.Revoke,true)]
    [InlineData("Admin","Member",MembershipAction.Revoke,true)]
    [InlineData("Admin","Admin",MembershipAction.Revoke,false)]
    [InlineData("Admin","Member",MembershipAction.Promote,false)]
    [InlineData("Admin","Admin",MembershipAction.Demote,false)]
    [InlineData("Owner","Owner",MembershipAction.Revoke,false)]
    [InlineData("Owner","Owner",MembershipAction.Demote,false)]
    [InlineData("Owner","Owner",MembershipAction.Promote,false)]
    [InlineData("Owner","Admin",MembershipAction.Promote,false)]
    [InlineData("Owner","Member",MembershipAction.Demote,false)]
    [InlineData("Member","Member",MembershipAction.Read,false)]
    [InlineData("Member","Member",MembershipAction.Revoke,false)]
    [InlineData("PlatformOperator","Member",MembershipAction.Read,false)]
    [InlineData("Owner","Owner",MembershipAction.Read,true)]
    [InlineData("Admin","Admin",MembershipAction.Read,true)]
    public void C08_matrix(string actor,string target,MembershipAction action,bool expected)=>
        Assert.Equal(expected,TenantMembershipPolicy.Allows(new(Account,Org,actor),Membership(target),action));

    [Fact]
    public void Scope_self_inactive_and_unknown_action_fail_closed()
    {
        var actor=new EnterpriseContext(Account,Org,"Owner");var target=Membership();
        foreach(var denied in new[]{target with{OrganizationId=Guid.NewGuid()},target with{AccountId=Account},target with{Status="Revoked"},target with{AccountActive=false}})
            Assert.False(TenantMembershipPolicy.Allows(actor,denied,MembershipAction.Revoke));
        Assert.False(TenantMembershipPolicy.Allows(actor,target,(MembershipAction)999));
    }
    [Fact]
    public void Version_is_canonical_strong_and_item_bound()
    {
        var target=Membership();var etag=MembershipVersion.Encode(target);
        Assert.True(MembershipVersion.TryDecode(etag,target,out var version));Assert.Equal(1,version);
        foreach(var text in new[]{"*","W/"+etag,etag+","+etag,"\"garbage\"",MembershipVersion.Encode(target with{MembershipId=Guid.NewGuid()}),MembershipVersion.Encode(target with{OrganizationId=Guid.NewGuid()})})
            Assert.False(MembershipVersion.TryDecode(text,target,out _));
    }
    private sealed class Authentication : IAuthenticatedSubject { public OidcSubject? GetSubject()=>new("https://synthetic.invalid","owner"); }
    private sealed class Context : IEnterpriseContextReader
    {
        public string Role="Owner"; public bool Active=true; public bool Provisioned=true;
        public bool IsProvisioned=>Provisioned;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject,CancellationToken token)=>Task.FromResult(Result.Success(
            new EnterpriseContextSnapshot(Identity,new(BindingHistoryState.Consumed,Org),[new(Identity,Account,Org,Account,Org,Active,Active,Active,Role)])));
    }
    private sealed class Store : ITenantMembershipStore
    {
        public TenantMembership Value=Membership();public int Reads,Writes;
        public Task<Result<TenantMembership>> ExecuteAsync(OidcSubject subject,Guid org,Guid member,MembershipAction action,long version,Guid operation,CancellationToken token)
        {
            if(org!=Value.OrganizationId || member!=Value.MembershipId) return Task.FromResult(Result.Failure<TenantMembership>(new("Resource.NotFound","Unavailable")));
            if(action==MembershipAction.Read) Reads++;
            else {
                Writes++;
                if(version!=Value.Version) return Task.FromResult(Result.Failure<TenantMembership>(new("Membership.VersionConflict","Unavailable")));
                Value=Value with{Role=action==MembershipAction.Promote?"Admin":action==MembershipAction.Demote?"Member":Value.Role,
                    Status=action==MembershipAction.Revoke?"Revoked":"Active",Version=Value.Version+1};
            }
            return Task.FromResult(Result.Success(Value));
        }
    }
    [Theory]
    [InlineData("Member",true,"Access.Forbidden")][InlineData("PlatformOperator",true,"Resource.NotFound")]
    [InlineData("Owner",false,"Resource.NotFound")]
    public async Task Use_case_denies_before_store_access(string role,bool active,string error)
    {
        var auth=new Authentication();var context=new Context{Role=role,Active=active};var store=new Store();
        var result=await new ManageTenantMembership(new(auth,context),auth,store).ExecuteAsync(Org,Member,MembershipAction.Revoke);
        Assert.Equal(error,result.Error.Code);Assert.Equal(0,store.Reads+store.Writes);
    }
    [Fact]
    public async Task Use_case_enforces_target_then_version_and_no_mutation_on_denial()
    {
        var auth=new Authentication();var store=new Store();var query=new ManageTenantMembership(new(auth,new Context()),auth,store);
        Assert.Equal("Membership.VersionRequired",(await query.ExecuteAsync(Org,Member,MembershipAction.Promote)).Error.Code);
        Assert.Equal("Membership.InvalidVersion",(await query.ExecuteAsync(Org,Member,MembershipAction.Promote,"*")).Error.Code);
        Assert.Equal(0,store.Writes);
        var tag=MembershipVersion.Encode(store.Value);
        Assert.True((await query.ExecuteAsync(Org,Member,MembershipAction.Promote,tag)).IsSuccess);
        Assert.Equal("Membership.VersionConflict",(await query.ExecuteAsync(Org,Member,MembershipAction.Demote,tag)).Error.Code);
        store.Value=store.Value with{Role="Owner"};
        Assert.Equal("Access.Forbidden",(await query.ExecuteAsync(Org,Member,MembershipAction.Revoke)).Error.Code);
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Request.Headers.ContainsKey("Test-Anonymous")
            ? Task.FromResult(AuthenticateResult.NoResult())
            : Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub","owner"),new("role","Owner"),new("organization_id",Guid.NewGuid().ToString())],Scheme.Name)),Scheme.Name)));
    }
    private sealed class Host(Context context,Store store):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only"}));
            builder.ConfigureServices(services=>{
                services.RemoveAll<IEnterpriseContextReader>();services.AddSingleton<IEnterpriseContextReader>(context);
                services.RemoveAll<ITenantMembershipStore>();services.AddSingleton<ITenantMembershipStore>(store);
                services.AddAuthentication(o=>{o.DefaultAuthenticateScheme="test";o.DefaultChallengeScheme="test";}).AddScheme<AuthenticationSchemeOptions,Handler>("test",_=>{});
            });
        }
    }
    [Fact]
    public async Task Http_item_and_commands_obey_role_version_and_contract()
    {
        var context=new Context();var store=new Store();await using var host=new Host(context,store);using var client=host.CreateClient();
        var path=$"/api/v1/organizations/{Org}/memberships/{Member}";
        var read=await client.GetAsync(path);Assert.Equal(HttpStatusCode.OK,read.StatusCode);Assert.NotNull(read.Headers.ETag);
        Assert.Equal((HttpStatusCode)428,(await client.PostAsync(path+"/promote",null)).StatusCode);
        using var malformed=new HttpRequestMessage(HttpMethod.Post,path+"/promote");malformed.Headers.TryAddWithoutValidation("If-Match","*");
        Assert.Equal(HttpStatusCode.BadRequest,(await client.SendAsync(malformed)).StatusCode);
        using var request=new HttpRequestMessage(HttpMethod.Post,path+"/promote");request.Headers.TryAddWithoutValidation("If-Match",read.Headers.ETag!.ToString());
        request.Content=new StringContent("{\"role\":\"Owner\",\"status\":\"Active\",\"organizationId\":\""+Guid.NewGuid()+"\"}");
        Assert.Equal(HttpStatusCode.OK,(await client.SendAsync(request)).StatusCode);
        Assert.Equal("Admin",store.Value.Role);Assert.Equal(Org,store.Value.OrganizationId);
        using var stale=new HttpRequestMessage(HttpMethod.Post,path+"/demote");stale.Headers.TryAddWithoutValidation("If-Match",read.Headers.ETag!.ToString());
        Assert.Equal(HttpStatusCode.PreconditionFailed,(await client.SendAsync(stale)).StatusCode);
        context.Role="Member";Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(path)).StatusCode);
        context.Role="Owner";Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(path.Replace(Org.ToString(),Guid.NewGuid().ToString()))).StatusCode);
        store.Value=store.Value with{Role="Owner"};Assert.Equal(HttpStatusCode.Forbidden,(await client.PostAsync(path+"/revoke",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.PatchAsync(path,new StringContent("{\"role\":\"Owner\",\"status\":\"Active\"}"))).StatusCode);
        client.DefaultRequestHeaders.Add("Test-Anonymous","true");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsync(path+"/revoke",null)).StatusCode);
    }
}
