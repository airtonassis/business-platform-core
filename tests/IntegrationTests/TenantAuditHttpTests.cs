using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
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

public sealed class TenantAuditHttpTests
{
    private static readonly Guid Org = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory log, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, log, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(
            Request.Headers.ContainsKey("Anonymous") ? AuthenticateResult.NoResult() : AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new Claim("iss", "https://synthetic.invalid"),
                new Claim("sub", "owner"), new Claim("organization_id", Guid.NewGuid().ToString()), new Claim("role", "Owner")], Scheme.Name)), Scheme.Name)));
    }
    private sealed class Context : IEnterpriseContextReader
    {
        public bool IsProvisioned => true;
        public string Role = "Owner";
        public string State = "Linked";
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject s, CancellationToken t)
        {
            var id = Guid.NewGuid(); var account = Guid.NewGuid();
            return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(id,
                new(State == "NotLinked" ? BindingHistoryState.NeverConsumed : BindingHistoryState.Consumed, State == "NotLinked" ? null : Org),
                State == "NotLinked" ? [] : [new(id, account, Org, account, Org, true, State != "Revoked", true, Role)])));
        }
    }
    private sealed class Reader : ITenantAuditReader
    {
        public int Calls;
        public bool Fail;
        public int Limit;
        public Task<Result<IReadOnlyList<TenantAuditEvent>>> ReadAsync(AuditAttestation attestation, CancellationToken t)
        {
            Calls++; Limit = int.Parse(attestation.GetPayload().Split('.')[6]);
            return Task.FromResult(Fail ? Result.Failure<IReadOnlyList<TenantAuditEvent>>(new("Dependency.Unavailable", "password=internal-secret")) :
                Result.Success<IReadOnlyList<TenantAuditEvent>>([new(Guid.NewGuid(), DateTimeOffset.UtcNow, "MembershipRevoked", Guid.NewGuid())]));
        }
    }
    private sealed class Host(Context context, Reader reader, bool keyConfigured=true) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only",
                ["AuditAttestation:ActiveKid"]=keyConfigured?"test":null,["AuditAttestation:ActiveKeyBase64"]=keyConfigured?Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)):null }));
            builder.ConfigureServices(s => {
                s.RemoveAll<IEnterpriseContextReader>(); s.AddSingleton<IEnterpriseContextReader>(context);
                s.RemoveAll<ITenantAuditReader>(); s.AddSingleton<ITenantAuditReader>(reader);
                s.AddAuthentication(o => { o.DefaultAuthenticateScheme="test"; o.DefaultChallengeScheme="test"; })
                    .AddScheme<AuthenticationSchemeOptions, Auth>("test", _ => { });
            });
        }
    }
    [Theory][InlineData("Owner")][InlineData("Admin")]
    public async Task Projection_and_default_limit(string role)
    {
        var reader = new Reader(); await using var host = new Host(new Context { Role=role }, reader); using var c=host.CreateClient();
        c.DefaultRequestHeaders.Add("X-Organization-ID",Guid.NewGuid().ToString());
        var response=await c.GetAsync($"/api/v1/organizations/{Org}/audit-events?organization_id={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode); Assert.Equal(25,reader.Limit);
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(new[]{"items","nextCursor"},json.RootElement.EnumerateObject().Select(x=>x.Name));
        Assert.Equal(new[]{"eventId","occurredAtUtc","kind","targetAccountId"},json.RootElement.GetProperty("items")[0].EnumerateObject().Select(x=>x.Name));
        Assert.Contains("no-store",response.Headers.CacheControl!.ToString());
    }
    [Theory][InlineData("Member","Linked",403)][InlineData("PlatformOperator","Linked",404)]
    [InlineData("Owner","NotLinked",404)][InlineData("Owner","Revoked",404)]
    public async Task Unauthorized_never_reads_audit(string role,string state,int status)
    {
        var reader=new Reader();await using var host=new Host(new Context{Role=role,State=state},reader);using var c=host.CreateClient();
        var response=await c.GetAsync($"/api/v1/organizations/{Org}/audit-events");
        Assert.Equal(status,(int)response.StatusCode);Assert.Equal(0,reader.Calls);
        Assert.DoesNotContain("targetAccountId",await response.Content.ReadAsStringAsync());
    }
    [Theory][InlineData("limit=0")][InlineData("limit=101")][InlineData("cursor=!")][InlineData("cursor=")]
    public async Task Invalid_pagination_is_bad_request(string query)
    {
        var reader=new Reader();await using var host=new Host(new Context(),reader);using var c=host.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest,(await c.GetAsync($"/api/v1/organizations/{Org}/audit-events?{query}")).StatusCode);
        Assert.Equal(0,reader.Calls);
    }
    [Fact]
    public async Task Cross_scope_and_revocation_between_pages_are_denied()
    {
        var reader=new Reader();var context=new Context();await using var host=new Host(context,reader);using var c=host.CreateClient();
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v1/organizations/{Guid.NewGuid()}/audit-events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.GetAsync($"/api/v1/organizations/{Org}/audit-events")).StatusCode);
        context.State="Revoked";
        Assert.Equal(HttpStatusCode.NotFound,(await c.GetAsync($"/api/v1/organizations/{Org}/audit-events")).StatusCode);Assert.Equal(1,reader.Calls);
    }
    [Fact]
    public async Task Missing_signing_key_fails_closed_without_audit_read()
    {
        var reader=new Reader();await using var host=new Host(new Context(),reader,keyConfigured:false);using var client=host.CreateClient();
        var response=await client.GetAsync($"/api/v1/organizations/{Org}/audit-events");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);Assert.Equal(0,reader.Calls);
    }
    [Fact]
    public async Task Infrastructure_errors_are_sanitized()
    {
        await using var host=new Host(new Context(),new Reader{Fail=true});using var c=host.CreateClient();
        var response=await c.GetAsync($"/api/v1/organizations/{Org}/audit-events");
        Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var body=await response.Content.ReadAsStringAsync();Assert.DoesNotContain("internal-secret",body);Assert.Contains("correlationId",body);
    }
}
