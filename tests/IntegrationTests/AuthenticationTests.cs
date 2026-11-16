using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Business.Platform.Core.Application.Operations;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class AuthenticationTests
{
    private sealed class Bootstrap : IAuthenticatedIdentityBootstrap { public int Calls; public Task RegisterAsync(OidcSubject s, CancellationToken c) { Calls++; return Task.CompletedTask; } }
    private sealed class AuditContext : IEnterpriseContextReader
    {
        public bool IsProvisioned => true;
        public Task<Business.Platform.Core.Domain.Shared.Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject s, CancellationToken t)
        {
            var id=Guid.NewGuid();var account=Guid.NewGuid();var org=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            return Task.FromResult(Business.Platform.Core.Domain.Shared.Result.Success(new EnterpriseContextSnapshot(id,
                new(BindingHistoryState.Consumed,org),[new(id,account,org,account,org,true,true,true,"Owner")])));
        }
    }
    private sealed class AuditReader : ITenantAuditReader
    {
        public Task<Business.Platform.Core.Domain.Shared.Result<IReadOnlyList<TenantAuditEvent>>> ReadAsync(AuditAttestation attestation,CancellationToken t)
            => Task.FromResult(Business.Platform.Core.Domain.Shared.Result.Success<IReadOnlyList<TenantAuditEvent>>([]));
    }
    // Generated keys exist only in test memory; never shipped or registered on the VPS.
    private sealed class Provider(bool mismatchedMetadata = false, bool audit = false) : WebApplicationFactory<Program>
    {
        public Bootstrap BootstrapSpy { get; } = new();
        public RSA Key { get; } = RSA.Create(2048);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Database:Host"] = "localhost", ["Database:Name"] = "business_core_g3",
                ["Database:Username"] = "bpc_test_runtime", ["Database:Password"] = "synthetic-only",
                ["AuditAttestation:ActiveKid"]="test",["AuditAttestation:ActiveKeyBase64"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IAuthenticatedIdentityBootstrap>(); services.AddSingleton<IAuthenticatedIdentityBootstrap>(BootstrapSpy);
                if(audit) {
                    services.RemoveAll<IEnterpriseContextReader>();services.AddSingleton<IEnterpriseContextReader,AuditContext>();
                    services.RemoveAll<ITenantAuditReader>();services.AddSingleton<ITenantAuditReader,AuditReader>();
                }
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => {
                options.Authority = "https://issuer.example";
                options.Audience = "core-api";
                options.TokenValidationParameters.ValidIssuer = options.Authority;
                options.TokenValidationParameters.ValidAudience = options.Audience;
                var config = new OpenIdConnectConfiguration { Issuer = mismatchedMetadata ? "https://wrong.example" : options.Authority };
                config.SigningKeys.Add(new RsaSecurityKey(Key.ExportParameters(false)) { KeyId = "synthetic" });
                options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(config);
            }); });
        }
        public string Token(string fault)
        {
            var signing = new SigningCredentials(new RsaSecurityKey(Key) { KeyId = "synthetic" }, SecurityAlgorithms.RsaSha256);
            var header = new JwtHeader(signing) { ["typ"] = fault == "id-token" ? "JWT" : "at+jwt" };
            var claims = new List<Claim> { new("sub", "subject-A"), new("client_id", "web"),
                new("jti", Guid.NewGuid().ToString()), new("iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64) };
            if (fault == "missing-subject") claims.RemoveAll(c => c.Type == "sub");
            var payload = new JwtPayload(fault == "issuer" ? "https://wrong.example" : "https://issuer.example",
                fault == "audience" ? "other-api" : "core-api", claims,
                DateTime.UtcNow.AddMinutes(-10), fault == "expired" ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5));
            var token = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
            return fault == "signature" ? token[..^8] + "AAAAAAAA" : token;
        }
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("signature")]
    [InlineData("id-token")]
    [InlineData("missing-subject")]
    public async Task Unsafe_or_wrong_purpose_tokens_are_rejected(string fault)
    {
        await using var provider = new Provider();
        using var client = provider.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", provider.Token(fault));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/context")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/audit-events")).StatusCode);
        provider.Key.Dispose();
    }

    [Fact]
    public async Task Valid_bearer_audit_query_never_bootstraps_identity()
    {
        await using var provider=new Provider(audit:true);using var client=provider.CreateClient();
        client.DefaultRequestHeaders.Authorization=new("Bearer",provider.Token("valid"));
        var response=await client.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/audit-events");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal(0,provider.BootstrapSpy.Calls);
        Assert.DoesNotContain("subject-A",await response.Content.ReadAsStringAsync());provider.Key.Dispose();
    }

    [Fact]
    public async Task Signed_access_token_authenticates_but_does_not_invent_a_business_endpoint()
    {
        await using var provider = new Provider();
        using var client = provider.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", provider.Token("valid"));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/me")).StatusCode);
        provider.Key.Dispose();
    }

    [Fact]
    public async Task Discovery_cannot_expand_trusted_issuer_to_a_different_authority()
    {
        await using var provider = new Provider(mismatchedMetadata: true);
        using var client = provider.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", provider.Token("issuer"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        provider.Key.Dispose();
    }
}
