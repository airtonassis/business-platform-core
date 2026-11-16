using Business.Platform.Core.Application.Operations;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Api.Authentication;
using Business.Platform.Core.Contracts.Operations;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Business.Platform.Core.Domain.Tenancy;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Infrastructure.Platform;

namespace Business.Platform.Core.Api;

public static class ApiComposition
{
    private static IResult ContextProblem(int status, string code) => Results.Problem(statusCode: status,
        title: "Context cannot be resolved.", extensions: new Dictionary<string, object?> { ["code"] = code });

    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.WebHost.ConfigureKestrel(options => {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = 65536;
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
        });
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context => {
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<SanitizedExceptionHandler>();
        builder.Services.AddSingleton(serviceProvider => {
            var options = new PostgresOptions();
            serviceProvider.GetRequiredService<IConfiguration>().GetSection("Database").Bind(options);
            return NpgsqlDataSource.Create(options.ConnectionString());
        });
        builder.Services.AddSingleton<IDatabaseReadiness, PostgresReadiness>();
        builder.Services.AddScoped<CheckDatabaseReadiness>();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IAuthenticatedSubject, HttpAuthenticatedSubject>();
        builder.Services.AddSingleton<IEnterpriseContextReader, PostgresEnterpriseContextReader>();
        builder.Services.AddSingleton<IAuthenticatedIdentityBootstrap, PostgresEnterpriseContextReader>();
        builder.Services.AddScoped<ResolveEnterpriseContext>();
        TenantAuditEndpoints.AddServices(builder.Services, builder.Configuration);
        TenantMembershipEndpoints.AddServices(builder.Services, builder.Configuration);
        InvitationEndpoints.AddServices(builder.Services);
        DemoRecordEndpoints.AddServices(builder.Services);
        builder.Services.AddSingleton<IIdentityProviderReadiness, OidcProviderReadiness>();
        builder.Services.AddScoped<CheckApplicationReadiness>();
        if(builder.Configuration.GetSection("PlatformDatabase").Exists()) builder.Services.AddSingleton(serviceProvider => {
            var options=new PostgresOptions();
            serviceProvider.GetRequiredService<IConfiguration>().GetSection("PlatformDatabase").Bind(options);
            return new PlatformDataSource(NpgsqlDataSource.Create(options.ConnectionString()));
        });
        builder.Services.AddSingleton<IPlatformStore,PostgresPlatformStore>();
        builder.Services.AddScoped<PlatformOperations>();
        PlatformEndpoints.AddLimits(builder.Services);
        var authority = builder.Configuration["Authentication:Authority"];
        var audience = builder.Configuration["Authentication:Audience"];
        if ((!string.IsNullOrWhiteSpace(authority) || !string.IsNullOrWhiteSpace(audience)) &&
            (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) || issuer.Scheme != "https" || string.IsNullOrWhiteSpace(audience)))
            throw new InvalidOperationException("OIDC configuration requires an HTTPS authority and audience.");
        builder.Services.AddAuthentication("RouteBearer").AddPolicyScheme("RouteBearer", "Route authentication", options => {
            options.ForwardDefaultSelector = context => Business.Platform.Core.Api.Web.BackChannelLogout.Enabled(context.RequestServices.GetRequiredService<IConfiguration>()) && Business.Platform.Core.Api.Web.BackChannelLogout.IsPath(context.Request.Path) ? "BackChannel" :
                context.Request.Path.StartsWithSegments("/api/v1/platform") ? "Platform" :
                Business.Platform.Core.Api.Web.WebComposition.Enabled(context.RequestServices.GetRequiredService<IConfiguration>()) &&
                (context.Request.Path.StartsWithSegments("/app") || context.Request.Path.StartsWithSegments("/account")) ? "Web" : JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options => {
            options.MapInboundClaims = false;
            options.IncludeErrorDetails = false;
            options.RequireHttpsMetadata = true;
            options.TokenValidationParameters = new TokenValidationParameters {
                ValidateIssuer = true, ValidateAudience = true, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, RequireSignedTokens = true, RequireExpirationTime = true,
                ValidTypes = ["at+jwt", "application/at+jwt"],
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSsaPssSha256, SecurityAlgorithms.EcdsaSha256],
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            if (!string.IsNullOrWhiteSpace(authority)) {
                options.Authority = authority; options.Audience = audience;
                options.TokenValidationParameters.ValidIssuer = authority;
            }
            var privateCa = builder.Configuration["Authentication:CertificateAuthorityBase64"];
            if (!string.IsNullOrWhiteSpace(privateCa)) options.BackchannelHttpHandler = PrivateOidcTrust.Handler(privateCa);
            options.BackchannelTimeout = TimeSpan.FromSeconds(4);
            options.Events = new JwtBearerEvents { OnTokenValidated = async context => {
                // Conservative RFC 9068 access-token adapter; no ID-token or email fallback.
                var mandatory = new[] { "iss", "sub", "client_id", "jti", "iat" };
                if (mandatory.Any(name => context.Principal!.FindAll(name).Count() != 1 ||
                    string.IsNullOrWhiteSpace(context.Principal.FindFirst(name)!.Value)) ||
                    !long.TryParse(context.Principal!.FindFirst("iat")!.Value, out var issued) ||
                    issued > DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds() ||
                    !string.Equals(context.SecurityToken.Issuer, context.Options.Authority, StringComparison.Ordinal))
                    { context.Fail("Invalid access token profile."); return; }
                // Authentication bootstrap is distinct from GET context; it never consumes a business account.
                if (context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<TenantAuditReadOnly>() is not null) return;
                try {
                    var bootstrap = context.HttpContext.RequestServices.GetRequiredService<IAuthenticatedIdentityBootstrap>();
                    await bootstrap.RegisterAsync(new OidcSubject(context.SecurityToken.Issuer,
                        context.Principal!.FindFirst("sub")!.Value), context.HttpContext.RequestAborted);
                } catch (Exception error) when (error is NpgsqlException or TimeoutException) {
                    // Keep cryptographic authentication valid; business dependency returns 503, never NotLinked.
                    context.HttpContext.Items["IdentityBootstrapUnavailable"] = true;
                }
            }, OnChallenge = async context => {
                context.HandleResponse();
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await Results.Problem(statusCode: 401, title: "Authentication required.",
                    extensions: new Dictionary<string, object?> { ["code"] = "Authentication.Required",
                        ["correlationId"] = context.HttpContext.TraceIdentifier }).ExecuteAsync(context.HttpContext);
            }};
        });
        PlatformAuthentication.Add(builder.Services,builder.Configuration);
        Business.Platform.Core.Api.Web.WebComposition.Add(builder.Services,builder.Configuration,builder.Environment);
        builder.Services.AddAuthorizationBuilder().SetFallbackPolicy(new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().Build());
        var app = builder.Build();
        // Validate configuration eagerly; constructing the data source opens no connection.
        _ = app.Services.GetRequiredService<NpgsqlDataSource>();
        app.Use(async (context, next) => {
            context.TraceIdentifier = Guid.NewGuid().ToString("D");
            context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
            context.Response.Headers.CacheControl = "no-store";
            await next(context);
        });
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context => {
            if (context.HttpContext.Response.StatusCode is 404 or 403)
                await Results.Problem(statusCode: context.HttpContext.Response.StatusCode,
                    title: "Request cannot be completed.").ExecuteAsync(context.HttpContext);
        });
        if(Business.Platform.Core.Api.Web.WebComposition.Enabled(app.Configuration)) {
            app.Use(async(context,next)=>{
                if(context.Request.Path.StartsWithSegments("/app") || context.Request.Path.StartsWithSegments("/account")) {
                    // Browsers apply form-action to the entire logout redirect chain.
                    // Trust only the server-configured HTTPS IdP origin, never request parameters.
                    var identityProviderOrigin=new Uri(app.Configuration["WebAuthentication:Authority"]!)
                        .GetComponents(UriComponents.SchemeAndServer,UriFormat.UriEscaped);
                    context.Response.Headers["Content-Security-Policy"]=$"default-src 'self'; script-src 'self'; style-src 'self'; form-action 'self' {identityProviderOrigin}; base-uri 'none'; frame-ancestors 'none'";
                    context.Response.Headers["Referrer-Policy"]="no-referrer";
                    if(!context.Request.IsHttps){context.Response.StatusCode=400;return;}
                }
                await next(context);
            });
            app.UseStaticFiles();
        }
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapGet("/health/live", () => Results.Ok(new HealthResponse("Healthy", "ProcessOnly"))).AllowAnonymous();
        app.MapGet("/health/database", async (CheckDatabaseReadiness check, CancellationToken token) =>
            await check.ExecuteAsync(token)
                ? Results.Ok(new HealthResponse("Healthy", "RestrictedDatabaseConnectivityOnly"))
                : Results.Json(new HealthResponse("Unhealthy", "Database"), statusCode: 503)).AllowAnonymous();
        app.MapGet("/health/ready", async (CheckApplicationReadiness check, CancellationToken token) => {
            var readiness = await check.ExecuteAsync(token);
            return Results.Json(new ReadinessResponse(readiness.IsReady ? "Ready" : "NotReady",
                new(readiness.Database, readiness.IdentityProvider, readiness.EnterprisePersistence)),
                statusCode: readiness.IsReady ? 200 : 503);
        }).AllowAnonymous();
        async Task<IResult> ReadContext(Guid? orgId, ResolveEnterpriseContext resolve, HttpContext http, ILoggerFactory logs, CancellationToken token) {
            if (http.Items.ContainsKey("IdentityBootstrapUnavailable")) return ContextProblem(503, "Dependency.Unavailable");
            var result = await resolve.ExecuteAsync(orgId, token);
            if (result.IsFailure) return ContextProblem(result.Error.Code == "Authentication.Required" ? 401 : 503, result.Error.Code);
            var decision = result.Value;
            if (decision.State == EnterpriseContextState.Inconsistent)
                logs.CreateLogger("EnterpriseContext").LogError("Enterprise context inconsistent. CorrelationId={CorrelationId}",http.TraceIdentifier);
            return decision.State switch {
                EnterpriseContextState.NotLinked => Results.Ok(new { state = "NotLinked", code = "Account.NotLinked" }),
                EnterpriseContextState.Allowed => Results.Ok(new { state = "Linked", accountId = decision.Context!.AccountId,
                    organizationId = decision.Context.OrganizationId, organizationName = decision.Context.OrganizationName,
                    membershipId = decision.Context.MembershipId, role = decision.Context.Role }),
                EnterpriseContextState.Denied => orgId.HasValue
                    ? ContextProblem(404, "Resource.NotFound") : ContextProblem(403, "Access.Unavailable"),
                _ => ContextProblem(500, "Context.Inconsistent")
            };
        }
        app.MapGet("/api/v1/me/context", (ResolveEnterpriseContext resolve, HttpContext http, ILoggerFactory logs, CancellationToken token) =>
            ReadContext(null, resolve, http, logs, token)).RequireAuthorization();
        app.MapGet("/api/v1/organizations/{orgId:guid}/context", (Guid orgId, ResolveEnterpriseContext resolve, HttpContext http, ILoggerFactory logs, CancellationToken token) =>
            ReadContext(orgId, resolve, http, logs, token)).RequireAuthorization();
        PlatformEndpoints.Map(app);
        TenantAuditEndpoints.Map(app);
        TenantMembershipEndpoints.Map(app);
        InvitationEndpoints.Map(app);
        DemoRecordEndpoints.Map(app);
        Business.Platform.Core.Api.Web.WebComposition.Map(app);
        app.MapFallback(() => Results.NotFound()).RequireAuthorization();
        return app;
    }
}

internal sealed class SanitizedExceptionHandler(IProblemDetailsService problems,
    ILogger<SanitizedExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        // Never log raw exceptions, SQL, headers, connection strings or secret configuration.
        logger.LogError("Request failed. CorrelationId={CorrelationId} ExceptionType={ExceptionType}",
            context.TraceIdentifier, exception.GetType().Name);
        var sessionDependency=(context.Request.Path.StartsWithSegments("/app") || context.Request.Path.StartsWithSegments("/account")) &&
            exception is NpgsqlException or TimeoutException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException;
        var status=sessionDependency?503:500;
        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext {
            HttpContext = context,
            ProblemDetails = new() { Status = status, Title = sessionDependency?"Session service unavailable.":"An unexpected error occurred.",
                Extensions = { ["code"] = sessionDependency?"Session.Unavailable":"Unexpected.Failure", ["correlationId"] = context.TraceIdentifier } }
        });
    }
}
