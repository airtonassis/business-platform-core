using System.Security.Claims;
using System.Threading.RateLimiting;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.RateLimiting;

namespace Business.Platform.Core.Api;

/// <summary>HTTP boundary. Actor, roles and MFA never come from request DTOs or headers.</summary>
public static class PlatformEndpoints
{
    public sealed record PreviewRequest(Guid UserId, string? RecipientReference)
    { public override string ToString() => "PreviewRequest [redacted]"; }
    public sealed record CreationRequest(Guid UserId, string? RecipientReference, string? Name)
    { public override string ToString() => "CreationRequest [redacted]"; }

    public static void AddLimits(IServiceCollection services)
    {
        services.AddRateLimiter(options => {
            options.RejectionStatusCode = 429;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                PartitionedRateLimiter.Create<HttpContext,string>(http => Limited(http)
                    ? RateLimitPartition.GetFixedWindowLimiter("operator:" + http.User.FindFirstValue("sub"), _ => Window(10))
                    : RateLimitPartition.GetNoLimiter("none")),
                PartitionedRateLimiter.Create<HttpContext,string>(http => Limited(http)
                    ? RateLimitPartition.GetFixedWindowLimiter("ip:" + http.Connection.RemoteIpAddress, _ => Window(30))
                    : RateLimitPartition.GetNoLimiter("none")));
            options.OnRejected = async (context, token) => {
                if(context.Lease.TryGetMetadata(MetadataName.RetryAfter,out var retry))
                    context.HttpContext.Response.Headers.RetryAfter=Math.Ceiling(retry.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                await Problem(429,"Preview.RateLimited").ExecuteAsync(context.HttpContext);
            };
        });
    }
    internal sealed class PreviewLimit;
    private static bool Limited(HttpContext http) => http.GetEndpoint()?.Metadata.GetMetadata<PreviewLimit>() is not null;
    private static FixedWindowRateLimiterOptions Window(int count) => new() {
        PermitLimit=count, Window=TimeSpan.FromMinutes(1), QueueLimit=0, AutoReplenishment=true
    };

    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/me/recipient-references", async (HttpContext http, PlatformOperations operations, CancellationToken token) => {
            if(http.Items.ContainsKey("IdentityBootstrapUnavailable")) return Problem(503,"Dependency.Unavailable");
            // Only verified OIDC profile claims. Never disclose email or issuer/subject as a display name.
            var name = http.User.FindFirstValue("name") ?? "Recipient";
            var result = await operations.IssueAsync(Subject(http.User),name,token);
            return result.IsFailure ? Failure(result.Error) : Results.Ok(new {
                userId=result.Value.UserId, recipientReference=result.Value.Reference, expiresAt=result.Value.ExpiresAt
            });
        }).RequireAuthorization();
        var group=app.MapGroup("/api/v1/platform").RequireAuthorization("PlatformOperator");
        group.MapPost("/owner-preview", async (PreviewRequest request, PlatformOperations operations, CancellationToken token) => {
            var result=await operations.PreviewAsync(request.UserId,request.RecipientReference ?? "",token);
            return result.IsFailure ? Failure(result.Error) : Results.Ok(result.Value);
        }).WithMetadata(new PreviewLimit());
        group.MapPost("/organizations", async (CreationRequest request, HttpContext http, PlatformOperations operations, CancellationToken token) => {
            var keys=http.Request.Headers["Idempotency-Key"];
            var result=await operations.CreateAsync(Subject(http.User),request.UserId,request.RecipientReference ?? "",request.Name ?? "",
                keys.Count==1 ? keys[0] ?? "" : "",token);
            return result.IsFailure ? Failure(result.Error) : Results.Json(result.Value,statusCode:result.Value.Replayed ? 200 : 201);
        });
    }
    private static OidcSubject Subject(ClaimsPrincipal principal) => new(principal.FindFirstValue("iss")!,principal.FindFirstValue("sub")!);
    private static IResult Failure(Error error) => Problem(error.Code switch {
        "Recipient.Unavailable" => 404, "Access.Unavailable" => 403, "Idempotency.PayloadMismatch" => 409,
        "Reference.RateLimited" => 429, "Idempotency.KeyInvalid" or "Organization.NameInvalid" or "Identity.DisplayNameUnavailable" => 400,
        _ => 503
    },error.Code);
    private static IResult Problem(int status,string code) => Results.Problem(statusCode:status,title:"Operation cannot be completed.",
        extensions:new Dictionary<string,object?> { ["code"]=code });
}
