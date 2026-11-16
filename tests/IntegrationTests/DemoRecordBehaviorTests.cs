using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
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

public sealed partial class DemoRecordTests
{
    private static readonly Guid Org=Guid.NewGuid(),Account=Guid.NewGuid(),Identity=Guid.NewGuid();
    private sealed class Authentication:IAuthenticatedSubject { public OidcSubject? GetSubject()=>new("https://synthetic.invalid","owner"); }
    private sealed class Context:IEnterpriseContextReader
    {
        public string Role="Owner";public bool Active=true;public bool IsProvisioned=>true;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject,CancellationToken token)=>Task.FromResult(Result.Success(
            new EnterpriseContextSnapshot(Identity,new(BindingHistoryState.Consumed,Org),[new(Identity,Account,Org,Account,Org,Active,Active,Active,Role)])));
    }
    private sealed class Store:IDemoRecordStore
    {
        public DemoRecord Item=new(Guid.NewGuid(),Org,"original",1);public int Calls,Writes;
        private readonly Dictionary<Guid,DemoRecordCreated> receipts=[];
        private static Result<T> Error<T>(string code)=>Result.Failure<T>(new(code,"Unavailable"));
        public Task<Result<DemoRecord>> ReadAsync(OidcSubject actor,Guid org,Guid id,CancellationToken token)
        { Calls++;return Task.FromResult(org==Item.OrganizationId && id==Item.Id?Result.Success(Item):Error<DemoRecord>("Resource.NotFound")); }
        public Task<Result<DemoRecordPage>> ListAsync(OidcSubject actor,Guid org,int limit,int offset,CancellationToken token)
        { Calls++;return Task.FromResult(Result.Success(new DemoRecordPage(offset==0?[Item]:[],false))); }
        public Task<Result<DemoRecordCreated>> CreateAsync(OidcSubject actor,Guid org,string title,Guid key,CancellationToken token)
        {
            Calls++;
            if(receipts.TryGetValue(key,out var prior)) return Task.FromResult(prior.Item.Title==title?Result.Success(prior with{Replayed=true}):Error<DemoRecordCreated>("Idempotency.PayloadMismatch"));
            Writes++;Item=new(Guid.NewGuid(),org,title,1);var result=new DemoRecordCreated(Item,false);receipts[key]=result;return Task.FromResult(Result.Success(result));
        }
        public Task<Result<DemoRecord>> UpdateAsync(OidcSubject actor,Guid org,Guid id,string title,long version,CancellationToken token)
        {
            Calls++;if(version!=Item.Version) return Task.FromResult(Error<DemoRecord>("DemoRecord.VersionConflict"));
            Writes++;Item=Item with{Title=title,Version=version+1};return Task.FromResult(Result.Success(Item));
        }
    }
    private static ManageDemoRecords UseCase(Context ctx,Store store)
    { var auth=new Authentication();return new(new ResolveEnterpriseContext(auth,ctx),auth,store); }
    [Theory]
    [InlineData("Owner",true)][InlineData("Admin",true)][InlineData("Member",false)]
    public async Task Application_role_matrix(string role,bool writes)
    {
        var ctx=new Context{Role=role};var store=new Store();var use=UseCase(ctx,store);
        Assert.True((await use.ReadAsync(Org,store.Item.Id)).IsSuccess);
        Assert.True((await use.ListAsync(Org)).IsSuccess);
        Assert.Equal(writes,(await use.CreateAsync(Org,"title",Guid.NewGuid())).IsSuccess);
        Assert.Equal(writes,(await use.UpdateAsync(Org,store.Item.Id,"update",DemoRecordVersion.Encode(store.Item))).IsSuccess);
        if(!writes) Assert.Equal(0,store.Writes);
    }
    [Theory]
    [InlineData("Owner",false)][InlineData("Admin",false)][InlineData("PlatformOperator",true)]
    public async Task Revoked_or_platform_never_reaches_resource_store(string role,bool active)
    {
        var ctx=new Context{Role=role,Active=active};var store=new Store();var use=UseCase(ctx,store);
        Assert.True((await use.ListAsync(Org)).IsFailure);Assert.True((await use.ReadAsync(Org,store.Item.Id)).IsFailure);
        Assert.True((await use.CreateAsync(Org,"title",Guid.NewGuid())).IsFailure);Assert.True((await use.UpdateAsync(Org,store.Item.Id,"update",null)).IsFailure);
        Assert.Equal(0,store.Calls);
    }
    [Fact]
    public async Task Foreign_scope_and_bad_page_fail_before_resource_io()
    {
        var store=new Store();var use=UseCase(new(),store);
        Assert.Equal("Resource.NotFound",(await use.ListAsync(Guid.NewGuid())).Error.Code);
        foreach(var (limit,offset) in new[]{(0,0),(101,0),(25,-1),(25,10001)}) Assert.Equal("DemoRecord.InvalidPage",(await use.ListAsync(Org,limit,offset)).Error.Code);
        Assert.Equal(0,store.Calls);Assert.True((await use.ListAsync(Org,100,10000)).IsSuccess);
    }
    [Fact]
    public async Task Rejected_input_does_not_write_and_authorization_precedes_version()
    {
        var ctx=new Context();var store=new Store();var use=UseCase(ctx,store);
        Assert.Equal("DemoRecord.VersionRequired",(await use.UpdateAsync(Org,store.Item.Id,"title",null)).Error.Code);
        Assert.Equal("DemoRecord.InvalidVersion",(await use.UpdateAsync(Org,store.Item.Id,"title","*")).Error.Code);
        Assert.Equal("Resource.NotFound",(await use.UpdateAsync(Org,Guid.NewGuid(),"title",null)).Error.Code);
        Assert.True((await use.CreateAsync(Org," ",Guid.NewGuid())).IsFailure);Assert.True((await use.CreateAsync(Org,"title",Guid.Empty)).IsFailure);
        ctx.Role="Member";Assert.Equal("Access.Forbidden",(await use.UpdateAsync(Org,store.Item.Id,"title",null)).Error.Code);
        Assert.Equal(0,store.Writes);
    }
    private sealed class Handler(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(Request.Headers.ContainsKey("Test-Anonymous")?AuthenticateResult.NoResult():
            AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub","owner"),new("role","Owner"),new("organization_id",Guid.NewGuid().ToString())],Scheme.Name)),Scheme.Name)));
    }
    private sealed class Host(Context context,Store store):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only"}));
            builder.ConfigureServices(services=>{
                services.RemoveAll<IEnterpriseContextReader>();services.AddSingleton<IEnterpriseContextReader>(context);
                services.RemoveAll<IDemoRecordStore>();services.AddSingleton<IDemoRecordStore>(store);
                services.AddAuthentication(o=>{o.DefaultAuthenticateScheme="test";o.DefaultChallengeScheme="test";}).AddScheme<AuthenticationSchemeOptions,Handler>("test",_=>{});
            });
        }
    }
    [Fact]
    public async Task Http_create_get_page_patch_and_preconditions()
    {
        var store=new Store();var context=new Context();await using var host=new Host(context,store);using var client=host.CreateClient();
        var path=$"/api/v1/organizations/{Org}/demo-records";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(path,new{title="title"})).StatusCode);
        client.DefaultRequestHeaders.Add("Idempotency-Key",Guid.NewGuid().ToString());
        var created=await client.PostAsJsonAsync(path,new{title=" title "});Assert.Equal(HttpStatusCode.Created,created.StatusCode);Assert.NotNull(created.Headers.Location);Assert.NotNull(created.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync(path,new{title="title"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path,new{title="different"})).StatusCode);
        var item=path+"/"+store.Item.Id;var read=await client.GetAsync(item);Assert.Equal(HttpStatusCode.OK,read.StatusCode);
        Assert.Equal(created.Headers.ETag,read.Headers.ETag);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(path)).StatusCode);Assert.Equal(HttpStatusCode.BadRequest,(await client.GetAsync(path+"?offset=10001")).StatusCode);
        Assert.Equal((HttpStatusCode)428,(await client.PatchAsJsonAsync(item,new{title="updated"})).StatusCode);
        async Task<HttpResponseMessage> Patch(string tag,object body) {
            using var request=new HttpRequestMessage(HttpMethod.Patch,item){Content=JsonContent.Create(body)};request.Headers.TryAddWithoutValidation("If-Match",tag);return await client.SendAsync(request);
        }
        Assert.Equal(HttpStatusCode.BadRequest,(await Patch("*",new{title="updated"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await Patch(read.Headers.ETag!.ToString(),new{title="updated"})).StatusCode);
        Assert.Equal(HttpStatusCode.PreconditionFailed,(await Patch(read.Headers.ETag!.ToString(),new{title="overwrite"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await Patch(DemoRecordVersion.Encode(store.Item),new{title="attack",organization_id=Guid.NewGuid(),version=999})).StatusCode);
        context.Role="Member";Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(item)).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await client.PatchAsJsonAsync(item,new{title="attack"})).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(item.Replace(Org.ToString(),Guid.NewGuid().ToString()))).StatusCode);
        context.Active=false;Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(item)).StatusCode);
        client.DefaultRequestHeaders.Add("Test-Anonymous","true");Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(item)).StatusCode);
    }
}
