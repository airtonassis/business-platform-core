using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Platform;

/// <summary>Committed reference emission; the secret itself never leaves the application boundary for persistence.</summary>
public sealed record RecipientReferenceIssued(Guid UserId, DateTimeOffset ExpiresAt);

/// <summary>Minimal owner information disclosed by a valid recipient reference.</summary>
public sealed record OwnerPreview(Guid UserId, string DisplayName);

/// <summary>Committed organization bootstrap or a matching idempotent replay.</summary>
public sealed record PlatformCreation(Guid OrganizationId, Guid AccountId, Guid MembershipId, bool Replayed);

/// <summary>Narrow persistence ports; callers must authorize the real operator before Platform operations.</summary>
public interface IPlatformStore
{
    /// <summary>Emits a reference for the exact authenticated subject, persisting only its hash.</summary>
    Task<Result<RecipientReferenceIssued>> IssueAsync(OidcSubject subject, byte[] hash, string displayName, CancellationToken cancellationToken);

    /// <summary>Reads minimal recipient information without persisting caller identity or audit events.</summary>
    Task<Result<OwnerPreview>> PreviewAsync(Guid userId, byte[] hash, CancellationToken cancellationToken);

    /// <summary>Atomically creates the organization, membership, receipt, and attributable events.</summary>
    Task<Result<PlatformCreation>> CreateAsync(OidcSubject actor, Guid userId, byte[] hash, string name, Guid operation, CancellationToken cancellationToken);
}
