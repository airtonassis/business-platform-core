using System.Net;
using System.Text.Json;
using Business.Platform.Core.Application.Operations;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class HostTests
{
    private sealed class Database(bool available) : IDatabaseReadiness
    {
        public Task<bool> CheckAsync(CancellationToken cancellationToken) => Task.FromResult(available);
    }

    private sealed class BrokenDatabase : IDatabaseReadiness
    {
        public Task<bool> CheckAsync(CancellationToken cancellationToken) =>
            throw new IOException("synthetic-sensitive-detail-never-return");
    }

    private sealed class Host(bool databaseAvailable, bool broken = false) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> {
                ["Database:Host"] = "localhost", ["Database:Name"] = "business_core_g3",
                ["Database:Username"] = "bpc_test_runtime", ["Database:Password"] = "synthetic-not-real"
            }));
            builder.ConfigureServices(services => {
                services.RemoveAll<IDatabaseReadiness>();
                services.AddSingleton<IDatabaseReadiness>(broken ? new BrokenDatabase() : new Database(databaseAvailable));
            });
        }
    }

    [Fact]
    public async Task Liveness_does_not_depend_on_database_and_unready_dependencies_return_503()
    {
        await using var host = new Host(false);
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/live")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/database")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/health/ready")).StatusCode);
    }

    [Fact]
    public async Task Database_health_does_not_claim_application_or_identity_is_ready()
    {
        await using var host = new Host(true);
        using var client = host.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/database")).StatusCode);
        var response = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        using var readiness = JsonDocument.Parse(text);
        Assert.True(readiness.RootElement.GetProperty("checks").GetProperty("database").GetBoolean());
        Assert.False(readiness.RootElement.GetProperty("checks").GetProperty("identityProvider").GetBoolean());
        Assert.False(readiness.RootElement.GetProperty("checks").GetProperty("enterprisePersistence").GetBoolean());
        Assert.DoesNotContain("synthetic-not-real", text);
        Assert.DoesNotContain("localhost", text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("forged-token")]
    public async Task Business_routes_fail_closed_without_an_oidc_provider(string? token)
    {
        await using var host = new Host(true);
        using var client = host.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var response = await client.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/demo-records");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.False(response.Headers.Contains("Location"));
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Authentication.Required", json.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Correlation_is_server_generated_and_no_arbitrary_client_value_is_echoed()
    {
        await using var host = new Host(true);
        using var client = host.CreateClient();
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "untrusted-client-value");
        var response = await client.GetAsync("/health/live");
        Assert.True(Guid.TryParse(Assert.Single(response.Headers.GetValues("X-Correlation-ID")), out _));
    }

    [Theory]
    [InlineData("postgres")]
    [InlineData("g3_bootstrap")]
    public void Bootstrap_and_administrative_names_are_rejected(string username)
    {
        var options = new PostgresOptions { Host = "localhost", Name = "business_core_g3",
            Username = username, Password = "synthetic-not-real" };
        Assert.Throws<ArgumentException>(options.ConnectionString);
    }

    [Fact]
    public async Task Unexpected_error_has_sanitized_problem_details_and_correlation()
    {
        await using var host = new Host(false, broken: true);
        using var client = host.CreateClient();
        var response = await client.GetAsync("/health/database");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("synthetic-sensitive-detail", text);
        Assert.DoesNotContain("IOException", text);
        using var json = JsonDocument.Parse(text);
        Assert.Equal("Unexpected.Failure", json.RootElement.GetProperty("code").GetString());
        Assert.True(Guid.TryParse(json.RootElement.GetProperty("correlationId").GetString(), out _));
    }

    [Fact]
    public void Application_and_public_contracts_remain_independent_from_adapters()
    {
        var references = typeof(CheckDatabaseReadiness).Assembly.GetReferencedAssemblies().Select(a => a.Name!);
        Assert.DoesNotContain(references, name => name.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
            name == "Npgsql" || name.Contains("Infrastructure", StringComparison.Ordinal));
        var contracts = typeof(Business.Platform.Core.Contracts.Operations.HealthResponse).Assembly.GetReferencedAssemblies();
        Assert.DoesNotContain(contracts, name => name.Name!.StartsWith("Business.Platform.Core.", StringComparison.Ordinal));
    }
}
