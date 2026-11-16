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

public sealed class ProtectedContextTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var subject = Request.Headers["Test-Subject"].ToString();
            if (subject == "Anonymous") return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("iss", "https://synthetic.invalid"), new("role", "PlatformOperator"), new("organization_id", B.ToString()) };
            if (subject != "Invalid") claims.Add(new("sub", string.IsNullOrEmpty(subject) ? "A" : subject));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(
                new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class Reader : IEnterpriseContextReader
    {
        public bool IsProvisioned => true;
        public string Role { get; set; } = "Member";
        public string State { get; set; } = "Linked";
        public int Calls { get; private set; }
        public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject, CancellationToken token)
        {
            Calls++;
            if (State == "Unavailable") return Task.FromResult(Result.Failure<EnterpriseContextSnapshot>(new("Dependency.Unavailable", "secret database detail")));
            var org = subject.Subject == "B" ? B : A;
            var identity = Guid.NewGuid(); var account = Guid.NewGuid();
            var history = State == "NotLinked" ? new EnterpriseBindingHistory(BindingHistoryState.NeverConsumed, null)
                : new EnterpriseBindingHistory(State == "Inconsistent" ? BindingHistoryState.Unknown : BindingHistoryState.Consumed, org);
            EnterpriseBindingFacts[] bindings = State == "NotLinked" ? [] : [new(identity, account, org, account, org,
                State != "AccountRevoked", State != "Revoked", State != "OrganizationInactive", Role, "Own organization", Guid.NewGuid())];
            return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(identity, history, bindings, State != "IdentityRevoked")));
        }
    }
    private sealed class Host(Reader reader) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Database:Host"] = "localhost", ["Database:Name"] = "synthetic", ["Database:Username"] = "synthetic", ["Database:Password"] = "synthetic-only"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IEnterpriseContextReader>(); services.AddSingleton<IEnterpriseContextReader>(reader);
                services.AddAuthentication(options => { options.DefaultAuthenticateScheme = "test"; options.DefaultChallengeScheme = "test"; })
                    .AddScheme<AuthenticationSchemeOptions, Auth>("test", _ => { });
            });
        }
    }
    [Theory]
    [InlineData("Owner", "A")][InlineData("Admin", "A")][InlineData("Member", "A")][InlineData("Member", "B")]
    public async Task Own_context_uses_server_membership_despite_forged_claims_headers_and_query(string role, string subject)
    {
        var reader = new Reader { Role = role };
        await using var host = new Host(reader); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Subject", subject);
        client.DefaultRequestHeaders.Add("X-Organization-ID", B.ToString());
        client.DefaultRequestHeaders.Add("X-Tenant-Role", "Owner");
        var org = subject == "B" ? B : A;
        var response = await client.GetAsync($"/api/v1/organizations/{org}/context?organization_id={B}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(org, body.RootElement.GetProperty("organizationId").GetGuid());
        Assert.Equal(role, body.RootElement.GetProperty("role").GetString());
        Assert.Equal("Linked", body.RootElement.GetProperty("state").GetString());
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        Assert.Equal(1, reader.Calls);
    }
    [Theory]
    [InlineData("Linked", "PlatformOperator", 404)][InlineData("Linked", "", 404)]
    [InlineData("Linked", "owner", 404)][InlineData("NotLinked", "Member", 404)]
    [InlineData("Revoked", "Member", 404)][InlineData("IdentityRevoked", "Member", 404)]
    [InlineData("AccountRevoked", "Member", 404)][InlineData("OrganizationInactive", "Member", 404)]
    [InlineData("Inconsistent", "Member", 500)][InlineData("Unavailable", "Member", 503)]
    public async Task Denials_and_dependency_failures_never_expose_context(string state, string role, int expected)
    {
        await using var host = new Host(new Reader { State = state, Role = role }); using var client = host.CreateClient();
        var response = await client.GetAsync($"/api/v1/organizations/{A}/context");
        Assert.Equal(expected, (int)response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(A.ToString(), body); Assert.DoesNotContain("accountId", body);
        Assert.DoesNotContain("secret", body); Assert.DoesNotContain("NotLinked", body);
        Assert.Contains("correlationId", body);
    }
    [Theory][InlineData("A")][InlineData("B")]
    public async Task Cross_organization_is_hidden(string subject)
    {
        await using var host = new Host(new Reader()); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Subject", subject);
        var response = await client.GetAsync($"/api/v1/organizations/{(subject == "A" ? B : A)}/context");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(A.ToString(), body); Assert.DoesNotContain(B.ToString(), body);
    }
    [Theory][InlineData("Anonymous")][InlineData("Invalid")]
    public async Task Invalid_identity_does_not_read_storage(string subject)
    {
        var reader = new Reader(); await using var host = new Host(reader); using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("Test-Subject", subject);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync($"/api/v1/organizations/{A}/context")).StatusCode);
        Assert.Equal(0, reader.Calls);
    }
    private sealed class Subject : IAuthenticatedSubject { public OidcSubject GetSubject() => new("https://synthetic.invalid", "A"); }
    [Theory][InlineData("Owner", true)][InlineData("Admin", true)][InlineData("Member", true)]
    [InlineData("PlatformOperator", false)][InlineData("", false)][InlineData("owner", false)]
    public async Task Direct_application_call_enforces_tenant_roles(string role, bool allowed)
    {
        var result = await new ResolveEnterpriseContext(new Subject(), new Reader { Role = role }).ExecuteAsync(A, default);
        Assert.Equal(allowed ? EnterpriseContextState.Allowed : EnterpriseContextState.Denied, result.Value.State);
        if (!allowed) Assert.Null(result.Value.Context);
    }
    [Fact]
    public async Task Subsequent_request_revalidates_membership_after_revocation()
    {
        var reader = new Reader(); await using var host = new Host(reader); using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/organizations/{A}/context")).StatusCode);
        reader.State = "Revoked";
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/v1/organizations/{A}/context")).StatusCode);
        Assert.Equal(2, reader.Calls);
    }
}
