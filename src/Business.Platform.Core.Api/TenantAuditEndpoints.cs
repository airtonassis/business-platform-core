using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Contracts.Operations;
using Business.Platform.Core.Infrastructure.Persistence;
using Npgsql;

namespace Business.Platform.Core.Api;

public static class TenantAuditEndpoints
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ReadTenantAudit>();
        services.AddSingleton<IAuditAttestationIssuer>(_ => {
            byte[]? key=null;
            try { key=Convert.FromBase64String(configuration["AuditAttestation:ActiveKeyBase64"] ?? ""); }
            catch (FormatException) { /* Closed failure at use case, never log secret configuration. */ }
            return new AuditAttestationIssuer(configuration["AuditAttestation:ActiveKid"],key,TimeProvider.System);
        });
        services.AddSingleton<ITenantAuditReader, PostgresTenantAuditReader>();
        services.AddSingleton(_ => {
            if (!configuration.GetSection("AuditDatabase").Exists()) return new AuditDataSource(null);
            var options = new PostgresOptions();
            configuration.GetSection("AuditDatabase").Bind(options);
            var general = configuration["Database:Username"];
            var platform = configuration["PlatformDatabase:Username"];
            if (options.Username == general || options.Username == platform)
                throw new InvalidOperationException("Dedicated audit database configuration is required.");
            var connection = new NpgsqlConnectionStringBuilder(options.ConnectionString()) { ApplicationName = "business-platform-core-audit" };
            return new AuditDataSource(NpgsqlDataSource.Create(connection.ConnectionString));
        });
    }
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/organizations/{orgId:guid}/audit-events", async (Guid orgId, int? limit, string? cursor,
            ReadTenantAudit query, HttpContext http, CancellationToken cancellationToken) => {
            if (http.Items.ContainsKey("IdentityBootstrapUnavailable")) return Problem(503, "Dependency.Unavailable");
            var result = await query.ExecuteAsync(orgId, limit ?? 25, cursor, cancellationToken);
            if (result.IsFailure) return Problem(result.Error.Code switch {
                "Authentication.Required" => 401, "Resource.NotFound" => 404, "Access.Forbidden" => 403,
                "Audit.InvalidPage" => 400, "Context.Inconsistent" => 500, _ => 503
            }, result.Error.Code);
            return Results.Ok(new TenantAuditResponse(result.Value.Items.Select(x =>
                new TenantAuditEventResponse(x.EventId, x.OccurredAtUtc, x.Kind, x.TargetAccountId)).ToArray(), result.Value.NextCursor));
        }).WithMetadata(new TenantAuditReadOnly()).RequireAuthorization();
    }
    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status,
        title: "Audit query cannot be completed.", extensions: new Dictionary<string, object?> { ["code"] = code });
}

// Authentication validates tokens but must not bootstrap identities during this read-only query.
internal sealed class TenantAuditReadOnly { }
