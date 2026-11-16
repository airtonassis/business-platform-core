using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;

namespace Business.Platform.Core.IntegrationTests;

public sealed class WebUiTests
{
    internal static readonly Guid Org=Guid.NewGuid(),OtherOrg=Guid.NewGuid(),Account=Guid.NewGuid();
    internal sealed class Context:IEnterpriseContextReader,IAuthenticatedIdentityBootstrap
    {
        public bool IsProvisioned=>true;
        public Task RegisterAsync(OidcSubject subject,CancellationToken token)=>Task.CompletedTask;
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject,CancellationToken token)
        {
            if(subject.Subject=="new")return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(Guid.NewGuid(),new(BindingHistoryState.NeverConsumed,null),[])));
            var role=subject.Subject=="member"?"Member":subject.Subject=="admin"?"Admin":"Owner";
            var org=subject.Subject=="b"?OtherOrg:Org;var id=Guid.NewGuid();
            return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(id,new(BindingHistoryState.Consumed,org),[new(id,Account,org,Account,org,subject.Subject!="revoked",true,true,role)])));
        }
    }
    internal sealed class Store:IDemoRecordStore,ITenantMembershipList,ITenantMembershipStore,IInvitationStore
    {
        public DemoRecord Item=new(Guid.NewGuid(),Org,"Visible only in A",1);public int Writes;public string? InvitationFailure;
        public Task<Result<DemoRecord>> ReadAsync(OidcSubject actor,Guid org,Guid id,CancellationToken token)=>Task.FromResult(Item.OrganizationId==org && Item.Id==id?Result.Success(Item):Result.Failure<DemoRecord>(new("Resource.NotFound","Unavailable")));
        public Task<Result<DemoRecordPage>> ListAsync(OidcSubject actor,Guid org,int limit,int offset,CancellationToken token)=>Task.FromResult(Result.Success(new DemoRecordPage(org==Item.OrganizationId && offset==0?[Item]:[],false)));
        public Task<Result<DemoRecordCreated>> CreateAsync(OidcSubject actor,Guid org,string title,Guid key,CancellationToken token){Writes++;Item=new(Guid.NewGuid(),org,title,1);return Task.FromResult(Result.Success(new DemoRecordCreated(Item,false)));}
        public Task<Result<DemoRecord>> UpdateAsync(OidcSubject actor,Guid org,Guid id,string title,long version,CancellationToken token)
        {
            if(org!=Item.OrganizationId || id!=Item.Id)return Task.FromResult(Result.Failure<DemoRecord>(new("Resource.NotFound","Unavailable")));
            if(version!=Item.Version)return Task.FromResult(Result.Failure<DemoRecord>(new("DemoRecord.VersionConflict","Unavailable")));
            Writes++;Item=Item with{Title=title,Version=version+1};return Task.FromResult(Result.Success(Item));
        }
        private TenantMembership Member(Guid org)=>new(Item.Id,Guid.NewGuid(),org,"Member","Active",true,1);
        public Task<Result<TenantMembershipPage>> ReadAsync(OidcSubject actor,Guid org,int limit,int offset,CancellationToken token)=>Task.FromResult(Result.Success(new TenantMembershipPage([Member(org)],false)));
        public Task<Result<TenantMembership>> ExecuteAsync(OidcSubject actor,Guid org,Guid id,MembershipAction action,long version,Guid operation,CancellationToken token)=>Task.FromResult(Result.Success(Member(org)));
        private InvitationDetails Invite(Guid org,Guid id,string state="Pending")=>new(id,org,Guid.NewGuid(),"Member",state,1,"Empresa A",DateTimeOffset.UtcNow,DateTimeOffset.UtcNow.AddHours(24),null,null);
        public Task<Result<InvitationOutcome>> ManageAsync(OidcSubject actor,InvitationCommand cmd,CancellationToken token)=>Task.FromResult(InvitationFailure is null?Result.Success(new InvitationOutcome(Invite(cmd.OrganizationId,cmd.InvitationId==Guid.Empty?Guid.NewGuid():cmd.InvitationId,cmd.Action=="Cancel"?"Cancelled":"Pending"),false)):Result.Failure<InvitationOutcome>(new(InvitationFailure,"Unavailable")));
        public Task<Result<IReadOnlyList<InvitationDetails>>> ListAsync(OidcSubject actor,InvitationCommand cmd,CancellationToken token)=>Task.FromResult(Result.Success<IReadOnlyList<InvitationDetails>>([Invite(cmd.OrganizationId,Item.Id)]));
        public Task<Result<InvitationOutcome>> RecipientAsync(OidcSubject actor,Guid id,byte[] hash,bool accept,long version,CancellationToken token)=>Task.FromResult(Result.Success(new InvitationOutcome(Invite(Org,id,accept?"Accepted":"Pending"),false)));
    }
    // Cookie issuance exists ONLY in this test assembly, never in the application or normal environments.
    internal sealed class FixtureSignIn:IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>{
            app.Use(async(http,following)=>{
                if(http.Request.Path.StartsWithSegments("/__fixture",out var identity)) {
                    // A new callback arrives without the prior sign-out cookie in the real OIDC flow.
                    http.Request.Headers.Cookie=string.Empty;
                    var principal=new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub",identity.Value!.Trim('/'))],"Web"));
                    await http.SignInAsync("Web",principal);http.Response.StatusCode=204;return;
                }
                await following();
            });next(app);
        };
    }
    private sealed class BearerFixture(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder):AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub",Request.Headers["Test-Subject"].ToString()),new("role","Owner"),new("organization_id",Guid.NewGuid().ToString())],Scheme.Name)),Scheme.Name)));
    }
    internal sealed class Host(bool fixture=true,string? authority=null,bool bearerFixture=false):WebApplicationFactory<Program>
    {
        public Store Store {get;}=new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(new Dictionary<string,string?>{
                ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only",
                ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]=authority??"https://oidc.synthetic.invalid",
                ["WebAuthentication:ClientId"]="bpc-web-local",["WebAuthentication:AllowLoopbackHttp"]="true",["Logging:LogLevel:Default"]="Critical"}));
            builder.ConfigureServices(services=>{
                if(!services.Any(s=>s.ServiceType==typeof(Business.Platform.Core.Api.Web.WebTicketStore))) {
                    var webConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                        ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]=authority??"https://oidc.synthetic.invalid",
                        ["WebAuthentication:CertificateAuthorityBase64"]=Environment.GetEnvironmentVariable("BPC_E205_OIDC_CA"),
                        ["WebAuthentication:ClientId"]="bpc-web-local",["WebAuthentication:AllowLoopbackHttp"]="true"}).Build();
                    Business.Platform.Core.Api.Web.WebComposition.Add(services,webConfig,new Microsoft.Extensions.Hosting.Internal.HostingEnvironment{EnvironmentName="Development"});
                }
                var context=new Context();services.RemoveAll<IEnterpriseContextReader>();services.AddSingleton<IEnterpriseContextReader>(context);
                services.RemoveAll<IAuthenticatedIdentityBootstrap>();services.AddSingleton<IAuthenticatedIdentityBootstrap>(context);
                services.RemoveAll<IDemoRecordStore>();services.AddSingleton<IDemoRecordStore>(Store);
                services.RemoveAll<ITenantMembershipList>();services.AddSingleton<ITenantMembershipList>(Store);
                services.RemoveAll<ITenantMembershipStore>();services.AddSingleton<ITenantMembershipStore>(Store);
                services.RemoveAll<IInvitationStore>();services.AddSingleton<IInvitationStore>(Store);
                if(fixture)services.AddSingleton<IStartupFilter,FixtureSignIn>();
                if(bearerFixture)services.AddAuthentication(options=>{options.DefaultAuthenticateScheme="test-list";options.DefaultChallengeScheme="test-list";}).AddScheme<AuthenticationSchemeOptions,BearerFixture>("test-list",_=>{});
            });
        }
        public HttpClient Browser()=>CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
    }
    internal static string Hidden(string html,string name)=>WebUtility.HtmlDecode(Regex.Match(html,"name=\""+Regex.Escape(name)+"\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    private static async Task<HttpResponseMessage> Post(HttpClient browser,string path,string html,Dictionary<string,string>? values=null)
    {var form=values??[];form["__RequestVerificationToken"]=Hidden(html,"__RequestVerificationToken");return await browser.PostAsync(path,new FormUrlEncodedContent(form));}
    [Theory]
    [InlineData("/app")]
    [InlineData("/account/logout")]
    public async Task Form_policy_allows_only_configured_oidc_origin_for_browser_logout(string path)
    {
        await using var host=new Host(authority:"https://oidc.synthetic.invalid:9443/realms/local");
        using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");
        var response=await browser.GetAsync(path+"?issuer=https://untrusted.invalid&returnUrl=https://untrusted.invalid");
        var policy=response.Headers.GetValues("Content-Security-Policy").Single();
        Assert.Contains("form-action 'self' https://oidc.synthetic.invalid:9443;",policy);
        Assert.Contains("script-src 'self';",policy);
        Assert.DoesNotContain("/realms",policy);Assert.DoesNotContain("untrusted",policy);Assert.DoesNotContain("*",policy);
    }
    [Fact]
    public async Task Default_records_page_does_not_report_missing_optional_section()
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");
        var response=await browser.GetAsync("/app");var html=await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Contains("Visible only in A",html);
        Assert.DoesNotContain("The Section field is required",html);
    }
    [Fact]
    public async Task Cookie_is_not_bearer_and_csrf_is_mandatory()
    {
        await using var host=new Host();using var browser=host.Browser();
        Assert.Equal(HttpStatusCode.Redirect,(await browser.GetAsync("/app")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await browser.GetAsync("/web.css")).StatusCode);
        var signIn=await browser.GetAsync("/__fixture/owner");var cookie=signIn.Headers.GetValues("Set-Cookie").Single();
        Assert.Contains("secure",cookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("httponly",cookie,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("owner",cookie);
        var html=await browser.GetStringAsync("/app");Assert.Contains("Visible only in A",html);Assert.Contains("no-store",(await browser.GetAsync("/app")).Headers.CacheControl!.ToString());
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.GetAsync("/api/v1/me/context")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await browser.PostAsync("/app?handler=Create",new FormUrlEncodedContent(new Dictionary<string,string>{{"Title","CSRF"}}))).StatusCode);
        Assert.Equal(0,host.Store.Writes);
        var response=await Post(browser,"/app?handler=Create",html,new(){{"Title","created"},{"Key",Guid.NewGuid().ToString()}});
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Equal(1,host.Store.Writes);
    }
    [Theory]
    [InlineData("owner",true)][InlineData("admin",true)][InlineData("member",false)]
    public async Task Server_roles_control_rendering_and_commands(string actor,bool manager)
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/"+actor);
        var html=await browser.GetStringAsync("/app");Assert.Equal(manager,html.Contains("Criar registro"));
        Assert.Equal(manager,html.Contains("section=members"));
        var response=await Post(browser,"/app?handler=Create",html,new(){{"Title","role-test"},{"Key",Guid.NewGuid().ToString()}});
        Assert.Equal(manager?HttpStatusCode.Redirect:HttpStatusCode.Forbidden,response.StatusCode);
        var list=await browser.GetAsync("/app?section=members");Assert.Equal(manager?HttpStatusCode.OK:HttpStatusCode.Forbidden,list.StatusCode);
    }
    [Fact]
    public async Task Identity_change_hides_prior_tenant_and_old_forms_fail()
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");var old=await browser.GetStringAsync("/app");
        await browser.GetAsync("/__fixture/b");var current=await browser.GetStringAsync("/app");Assert.DoesNotContain("Visible only in A",current);
        Assert.Equal(HttpStatusCode.BadRequest,(await Post(browser,"/app?handler=Create",old,new(){{"Title","old tenant form"},{"Key",Guid.NewGuid().ToString()}})).StatusCode);
        var foreign=await browser.GetAsync("/app?item="+host.Store.Item.Id);Assert.Equal(HttpStatusCode.NotFound,foreign.StatusCode);Assert.DoesNotContain("Visible only in A",await foreign.Content.ReadAsStringAsync());
    }
    [Fact]
    public async Task Etag_and_input_errors_do_not_overwrite()
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");var html=await browser.GetStringAsync("/app?item="+host.Store.Item.Id);
        var id=host.Store.Item.Id.ToString();var tag=DemoRecordVersion.Encode(host.Store.Item);
        async Task<HttpResponseMessage> Patch(string? version)=>await Post(browser,"/app?handler=Update",html,new(){{"Title","changed"},{"Id",id},{"Version",version??""}});
        Assert.Equal(HttpStatusCode.BadRequest,(await Patch("*")).StatusCode);
        Assert.Equal((HttpStatusCode)428,(await Patch(null)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Patch(tag)).StatusCode);
        var stale=await Patch(tag);Assert.Equal(HttpStatusCode.PreconditionFailed,stale.StatusCode);Assert.Contains("nada foi sobrescrito",await stale.Content.ReadAsStringAsync());Assert.Equal(1,host.Store.Writes);
        Assert.Equal(HttpStatusCode.BadRequest,(await browser.GetAsync("/app?limit=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await browser.GetAsync("/app?offset=10001")).StatusCode);
    }
    [Fact]
    public async Task Invitation_pages_use_real_use_cases_without_secret_urls()
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");
        var html=await browser.GetStringAsync("/app?section=invitations");Assert.Contains("Emitir convite",html);
        var code=Business.Platform.Core.Application.Platform.OwnerReferenceCodec.Generate();
        var response=await Post(browser,"/app?handler=Issue",html,new(){{"Recipient",Guid.NewGuid().ToString()},{"RecipientReference",code},{"Key",Guid.NewGuid().ToString()}});
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Null(response.Headers.Location);Assert.Contains("Código mostrado",await response.Content.ReadAsStringAsync());
        Assert.DoesNotContain(code,await response.Content.ReadAsStringAsync());
        host.Store.InvitationFailure="Idempotency.PayloadMismatch";
        var conflict=await Post(browser,"/app?handler=Issue",html,new(){{"Recipient",Guid.NewGuid().ToString()},{"RecipientReference",code},{"Key",Guid.NewGuid().ToString()}});
        Assert.Equal(HttpStatusCode.Conflict,conflict.StatusCode);
    }
    [Fact]
    public async Task Session_extension_and_logout_revokes_cookie_copies()
    {
        await using var host=new Host();using var browser=host.Browser();var issued=await browser.GetAsync("/__fixture/owner");
        var oldCookie=issued.Headers.GetValues("Set-Cookie").Single().Split(';')[0];
        var html=await browser.GetStringAsync("/app");
        Assert.Equal(HttpStatusCode.BadRequest,(await browser.PostAsync("/account/session/continue",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await Post(browser,"/account/session/continue",html)).StatusCode);
        using var replay=host.Browser();replay.DefaultRequestHeaders.Add("Cookie",oldCookie);
        Assert.Equal(HttpStatusCode.OK,(await browser.GetAsync("/account/session")).StatusCode);
        var current=await browser.GetStringAsync("/app");
        // Test fixture has no reachable IdP. Local revocation precedes any remote logout failure.
        await Post(browser,"/account/logout",current);
        Assert.Equal(HttpStatusCode.Unauthorized,(await browser.GetAsync("/account/session")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect,(await replay.GetAsync("/app")).StatusCode);
    }
    [Theory]
    [InlineData("owner",true)][InlineData("admin",true)][InlineData("member",false)]
    public async Task Membership_list_http_uses_authoritative_role_and_scope(string actor,bool manager)
    {
        await using var host=new Host(bearerFixture:true);using var browser=host.Browser();browser.DefaultRequestHeaders.Add("Test-Subject",actor);
        var path=$"/api/v1/organizations/{Org}/memberships";var response=await browser.GetAsync(path);
        Assert.Equal(manager?HttpStatusCode.OK:HttpStatusCode.Forbidden,response.StatusCode);
        if(manager){var json=await response.Content.ReadAsStringAsync();Assert.Contains("resourceVersion",json);Assert.DoesNotContain("issuer",json);Assert.DoesNotContain("subject",json);Assert.Equal(HttpStatusCode.BadRequest,(await browser.GetAsync(path+"?limit=101")).StatusCode);}
        Assert.Equal(HttpStatusCode.NotFound,(await browser.GetAsync($"/api/v1/organizations/{OtherOrg}/memberships")).StatusCode);
    }
    [Fact]
    public async Task Invitation_cancel_replace_preview_and_accept_preserve_forms_and_versions()
    {
        await using var host=new Host();using var browser=host.Browser();await browser.GetAsync("/__fixture/owner");
        var id=host.Store.Item.Id.ToString();var html=await browser.GetStringAsync("/app?section=invitations&item="+id);
        var version=Hidden(html,"Version");Assert.NotEmpty(version);
        var cancelled=await Post(browser,"/app?handler=ChangeInvitation",html,new(){{"Id",id},{"Version",version},{"replace","false"}});
        Assert.Equal(HttpStatusCode.OK,cancelled.StatusCode);Assert.Contains("Cancelled",await cancelled.Content.ReadAsStringAsync());
        var replaced=await Post(browser,"/app?handler=ChangeInvitation",html,new(){{"Id",id},{"Version",version},{"replace","true"},{"Key",Guid.NewGuid().ToString()}});
        Assert.Equal(HttpStatusCode.OK,replaced.StatusCode);Assert.Contains("Código mostrado",await replaced.Content.ReadAsStringAsync());
        await browser.GetAsync("/__fixture/new");var recipient=await browser.GetStringAsync("/app");Assert.Contains("Preparar meu acesso",recipient);
        var code=Business.Platform.Core.Application.Platform.OwnerReferenceCodec.Generate();
        var preview=await Post(browser,"/app?handler=Recipient",recipient,new(){{"Id",id},{"Code",code},{"accept","false"}});
        Assert.Equal(HttpStatusCode.OK,preview.StatusCode);var previewHtml=await preview.Content.ReadAsStringAsync();
        Assert.Contains("Prévia conferida",WebUtility.HtmlDecode(previewHtml));Assert.DoesNotContain(code,previewHtml);Assert.DoesNotContain("Criar registro",previewHtml);
        var accepted=await Post(browser,"/app?handler=Recipient",previewHtml,new(){{"Id",id},{"Code",code},{"Version",Hidden(previewHtml,"Version")},{"accept","true"}});
        Assert.Equal(HttpStatusCode.OK,accepted.StatusCode);Assert.Contains("Convite aceito",await accepted.Content.ReadAsStringAsync());Assert.DoesNotContain(code,await accepted.Content.ReadAsStringAsync());
    }
}
