using System.Security.Cryptography;
using System.Text;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Platform;

/// <summary>One-time response carrying the opaque secret; diagnostic formatting is redacted.</summary>
public sealed record IssuedOwnerReference(Guid UserId, string Reference, DateTimeOffset ExpiresAt)
{
    /// <inheritdoc />
    public override string ToString() => "IssuedOwnerReference [redacted]";
}

/// <summary>Canonical unpadded base64url references with exactly 256 bits of random entropy.</summary>
public static class OwnerReferenceCodec
{
    /// <summary>Creates a cryptographically random reference; callers must never log its plaintext.</summary>
    public static string Generate() => Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>Accepts only canonical 32-byte references and returns a SHA-256 digest for persistence.</summary>
    public static bool TryHash(string? reference, out byte[] hash)
    {
        hash = [];
        if (reference is null || reference.Length != 43 || reference.Any(c =>
            !(c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_')))
            return false;
        Span<byte> bytes = stackalloc byte[32];
        if (!Convert.TryFromBase64String(reference.Replace('-', '+').Replace('_', '/') + "=", bytes, out var written)
            || written != 32 || Encode(bytes) != reference) return false;
        hash = SHA256.HashData(bytes);
        CryptographicOperations.ZeroMemory(bytes);
        return true;
    }

    private static string Encode(ReadOnlySpan<byte> bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>Stable operation identifiers independent of request payload, allowing SQL to detect payload conflicts.</summary>
public static class PlatformOperationId
{
    /// <summary>Validates a case-sensitive visible ASCII key and derives a domain-separated identifier.</summary>
    public static bool TryCreate(string? key, out Guid operation)
    {
        operation = Guid.Empty;
        if (key is null || key.Length is < 1 or > 128 || key.Any(c => c is < '!' or > '~')) return false;
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes("bpc.platform.create.v1\0" + key));
        operation = new Guid(digest.AsSpan(0, 16));
        return true;
    }
}

/// <summary>
/// Application operations for trusted authentication adapters. Issue requires the validated token subject
/// and a server-verified display name. Before every Preview/Create, the API must verify the real
/// operator's Platform role and MFA. A supplied OIDC subject alone is not evidence of authorization.
/// Cancellation during a write may leave an unconfirmed outcome; callers must retain the same key.
/// </summary>
public sealed class PlatformOperations(IPlatformStore store)
{
    /// <summary>Emits a secret only after persistence confirms the reference transaction.</summary>
    public async Task<Result<IssuedOwnerReference>> IssueAsync(OidcSubject subject, string verifiedDisplayName, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(verifiedDisplayName) || verifiedDisplayName.Trim().Length > 100)
            return Result.Failure<IssuedOwnerReference>(new("Identity.DisplayNameUnavailable", "A verified display name is required.", ErrorType.Validation));
        var reference = OwnerReferenceCodec.Generate();
        OwnerReferenceCodec.TryHash(reference, out var hash);
        var result = await store.IssueAsync(subject, hash, verifiedDisplayName.Trim(), cancellationToken);
        return result.IsFailure ? Result.Failure<IssuedOwnerReference>(result.Error)
            : Result.Success(new IssuedOwnerReference(result.Value.UserId, reference, result.Value.ExpiresAt));
    }

    /// <summary>Resolves a reference after the trusted boundary verifies operator authorization.</summary>
    public Task<Result<OwnerPreview>> PreviewAsync(Guid userId, string reference, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || !OwnerReferenceCodec.TryHash(reference, out var hash))
            return Task.FromResult(Result.Failure<OwnerPreview>(RecipientUnavailable()));
        return store.PreviewAsync(userId, hash, cancellationToken);
    }

    /// <summary>Creates or replays using the exact authorized actor and stable client operation key.</summary>
    public Task<Result<PlatformCreation>> CreateAsync(OidcSubject actor, Guid userId, string reference,
        string organizationName, string idempotencyKey, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (userId == Guid.Empty || !OwnerReferenceCodec.TryHash(reference, out var hash))
            return Task.FromResult(Result.Failure<PlatformCreation>(RecipientUnavailable()));
        if (string.IsNullOrWhiteSpace(organizationName) || organizationName.Trim().Length > 200)
            return Task.FromResult(Result.Failure<PlatformCreation>(new("Organization.NameInvalid", "Organization name must contain between 1 and 200 characters.", ErrorType.Validation)));
        if (!PlatformOperationId.TryCreate(idempotencyKey, out var operation))
            return Task.FromResult(Result.Failure<PlatformCreation>(new("Idempotency.KeyInvalid", "An idempotency key of 1 to 128 visible ASCII characters is required.", ErrorType.Validation)));
        return store.CreateAsync(actor, userId, hash, organizationName.Trim(), operation, cancellationToken);
    }

    private static Error RecipientUnavailable() => new("Recipient.Unavailable", "Recipient reference unavailable.", ErrorType.NotFound);
}
