using System.Globalization;
using System.Text;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Public projection, deliberately excludes actors and operation identifiers.</summary>
public sealed record TenantAuditEvent(Guid EventId, DateTimeOffset OccurredAtUtc, string Kind, Guid TargetAccountId);
/// <summary>A page has no authorization state; every request revalidates its caller.</summary>
public sealed record TenantAuditPage(IReadOnlyList<TenantAuditEvent> Items, string? NextCursor);
/// <summary>Typed, precision-preserving keyset position; never a tenant authority.</summary>
public sealed record TenantAuditPosition(DateTimeOffset Time, Guid EventId)
{
    /// <summary>Canonical opaque transport encoding with a version and no identity.</summary>
    public string Encode() => Convert.ToBase64String(Encoding.UTF8.GetBytes(
        $"1|{Time.UtcTicks.ToString(CultureInfo.InvariantCulture)}|{EventId:D}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    /// <summary>Rejects oversized, noncanonical and sub-microsecond cursor values.</summary>
    public static bool TryDecode(string? cursor, out TenantAuditPosition? position)
    {
        position = null;
        if (cursor is null) return true;
        if (cursor.Length is < 1 or > 128) return false;
        try {
            var encoded = cursor.Replace('-', '+').Replace('_', '/');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(encoded.PadRight((encoded.Length + 3) / 4 * 4, '='))).Split('|');
            if (parts.Length != 3 || parts[0] != "1" ||
                !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) || ticks % 10 != 0 ||
                !Guid.TryParseExact(parts[2], "D", out var id) || id == Guid.Empty) return false;
            var candidate = new TenantAuditPosition(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            if (candidate.Encode() != cursor) return false;
            position = candidate; return true;
        } catch (FormatException) { return false; }
        catch (ArgumentOutOfRangeException) { return false; }
    }
}
/// <summary>Read-only port. SQL must revalidate identity and membership in the read operation.</summary>
public interface ITenantAuditReader
{
    /// <summary>Returns at most limit+1 rows in descending timestamp/UUID order.</summary>
    Task<Result<IReadOnlyList<TenantAuditEvent>>> ReadAsync(AuditAttestation attestation, CancellationToken cancellationToken);
}
/// <summary>Tenant audit authorization shared by HTTP and non-HTTP consumers.</summary>
public sealed class ReadTenantAudit(ResolveEnterpriseContext resolve, IAuthenticatedSubject authentication, ITenantAuditReader reader,
    IAuditAttestationIssuer issuer)
{
    /// <summary>Resolves trusted context on every page; a route and cursor cannot grant access.</summary>
    public async Task<Result<TenantAuditPage>> ExecuteAsync(Guid organizationId, int limit = 25, string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await resolve.ExecuteAsync(organizationId, cancellationToken);
        if (resolved.IsFailure) return Result.Failure<TenantAuditPage>(resolved.Error);
        var decision = resolved.Value;
        if (decision.State == EnterpriseContextState.Inconsistent) return Failure("Context.Inconsistent");
        if (decision.State != EnterpriseContextState.Allowed) return Failure("Resource.NotFound");
        if (decision.Context!.Role is not ("Owner" or "Admin")) return Failure("Access.Forbidden");
        if (limit is < 1 or > 100 || !TenantAuditPosition.TryDecode(cursor, out var position)) return Failure("Audit.InvalidPage");
        var subject = authentication.GetSubject();
        if (subject is null) return Failure("Authentication.Required");
        var attestation = issuer.Issue(subject, decision.Context.OrganizationId, limit, cursor);
        if (attestation.IsFailure) return Result.Failure<TenantAuditPage>(attestation.Error);
        var loaded = await reader.ReadAsync(attestation.Value, cancellationToken);
        if (loaded.IsFailure) return Result.Failure<TenantAuditPage>(loaded.Error);
        var items = loaded.Value.Take(limit).ToArray();
        return Result.Success(new TenantAuditPage(items, loaded.Value.Count > limit
            ? new TenantAuditPosition(items[^1].OccurredAtUtc, items[^1].EventId).Encode() : null));
    }
    private static Result<TenantAuditPage> Failure(string code) => Result.Failure<TenantAuditPage>(new(code, "Audit query cannot be completed."));
}
