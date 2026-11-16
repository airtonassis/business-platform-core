using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Application.Tenancy;

/// <summary>Only an authenticated adapter may provide this subject. No client tenant or role is accepted.</summary>
public interface IAuthenticatedSubject
{
    /// <summary>Returns the validated issuer/subject, or null when unauthenticated.</summary>
    OidcSubject? GetSubject();
}

/// <summary>Authoritative facts from one consistent transaction, including permanent historical consumption.</summary>
public sealed record EnterpriseContextSnapshot(Guid IdentityId, EnterpriseBindingHistory History,
    IReadOnlyList<EnterpriseBindingFacts> Bindings, bool IdentityActive = true);

/// <summary>Port for a future approved persistence mechanism. Never infer history from active rows alone.</summary>
public interface IEnterpriseContextReader
{
    /// <summary>True only for a real implemented adapter backed by approved migrations, not a configuration override.</summary>
    bool IsProvisioned { get; }
    /// <summary>Checks the real migration dependency; default supports non-database adapters.</summary>
    Task<bool> CheckProvisionedAsync(CancellationToken cancellationToken) => Task.FromResult(IsProvisioned);
    /// <summary>Returns an atomic snapshot; absence of schema is dependency failure, never NotLinked.</summary>
    Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject, CancellationToken cancellationToken);
}

/// <summary>Resolves approved enterprise rules without defining unapproved transport or storage contracts.</summary>
public sealed class ResolveEnterpriseContext(IAuthenticatedSubject authentication, IEnterpriseContextReader reader)
{
    /// <summary>Revalidates on every invocation and returns no context for missing authentication/storage.</summary>
    public async Task<Result<EnterpriseContextDecision>> ExecuteAsync(Guid? requestedOrganization, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var subject = authentication.GetSubject();
        if (subject is null) return Result.Failure<EnterpriseContextDecision>(new("Authentication.Required", "Authentication required.", ErrorType.Unauthorized));
        if (!reader.IsProvisioned) return Result.Failure<EnterpriseContextDecision>(new("Enterprise.PersistencePending", "Enterprise persistence is not provisioned."));
        var loaded = await reader.ReadAsync(subject, cancellationToken);
        if (loaded.IsFailure) return Result.Failure<EnterpriseContextDecision>(loaded.Error);
        var snapshot = loaded.Value;
        return Result.Success(EnterpriseContextPolicy.Evaluate(snapshot.IdentityId, snapshot.History,
            snapshot.Bindings, requestedOrganization, snapshot.IdentityActive));
    }
}
