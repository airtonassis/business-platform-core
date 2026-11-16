using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Internal transport capability; deliberately redacted in strings and JSON.</summary>
public sealed class AuditAttestation(string payload, byte[] mac)
{
    /// <summary>Internal canonical bytes represented as ASCII; never public HTTP data.</summary>
    public string GetPayload() => payload;
    /// <summary>Authentication tag; never log or serialize.</summary>
    private readonly byte[] tag = (byte[])mac.Clone();
    /// <summary>Returns a copy for the internal database transport.</summary>
    public byte[] GetMac() => (byte[])tag.Clone();
    /// <inheritdoc />
    public override string ToString() => "AuditAttestation [redacted]";
}
/// <summary>Issues only after the calling use case has resolved and authorized its context.</summary>
public interface IAuditAttestationIssuer
{
    /// <summary>Issues a bounded capability for an already-authorized context.</summary>
    Result<AuditAttestation> Issue(OidcSubject subject, Guid organizationId, int limit, string? exactCursor);
}
/// <summary>Canonical ASCII protocol v1; key is independent from SQL credentials and never returned.</summary>
public sealed class AuditAttestationIssuer : IAuditAttestationIssuer
{
    private readonly string? kid;
    private readonly byte[]? secret;
    private readonly TimeProvider clock;
    /// <summary>Copies a runtime-injected key; missing or malformed configuration fails closed.</summary>
    public AuditAttestationIssuer(string? activeKid, byte[]? activeKey, TimeProvider timeProvider)
    {
        kid = activeKid; secret = activeKey is null ? null : (byte[])activeKey.Clone(); clock = timeProvider;
    }
    /// <inheritdoc />
    public Result<AuditAttestation> Issue(OidcSubject subject, Guid organizationId, int limit, string? exactCursor)
    {
        if (secret?.Length != 32 || string.IsNullOrEmpty(kid) || kid.Length > 64 ||
            kid.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-')) ||
            organizationId == Guid.Empty || limit is < 1 or > 100 ||
            subject.Issuer.Contains('\0') || subject.Subject.Contains('\0') ||
            !TenantAuditPosition.TryDecode(exactCursor, out _))
            return Result.Failure<AuditAttestation>(new("Audit.AttestationUnavailable", "Audit dependency unavailable."));
        var issued = clock.GetUtcNow().ToUnixTimeSeconds();
        if (issued < 0 || issued > 999999999969)
            return Result.Failure<AuditAttestation>(new("Audit.AttestationUnavailable", "Audit dependency unavailable."));
        static string B64(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).TrimEnd('=').Replace('+','-').Replace('/','_');
        var payload = string.Join('.', "v1", kid, B64(subject.Issuer), B64(subject.Subject), organizationId.ToString("D"),
            "tenant-audit-read", limit.ToString(CultureInfo.InvariantCulture), exactCursor ?? "~",
            issued.ToString(CultureInfo.InvariantCulture), (issued+30).ToString(CultureInfo.InvariantCulture));
        if (Encoding.UTF8.GetByteCount(payload)>8192)
            return Result.Failure<AuditAttestation>(new("Audit.AttestationUnavailable", "Audit dependency unavailable."));
        return Result.Success(new AuditAttestation(payload, HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes(payload))));
    }
    /// <inheritdoc />
    public override string ToString() => "AuditAttestationIssuer [redacted]";
}
