using Business.Platform.Core.Application.Tenancy;

namespace Business.Platform.Core.Application.Operations;

/// <summary>Provider-neutral discovery readiness; never validates a user or grants enterprise access.</summary>
public interface IIdentityProviderReadiness
{
    /// <summary>Checks configured trusted issuer and signing metadata, without exposing their contents.</summary>
    Task<bool> CheckAsync(CancellationToken cancellationToken);
}

/// <summary>Independent technical prerequisites; no environment flag can approve enterprise persistence.</summary>
public sealed record ApplicationReadiness(bool Database, bool IdentityProvider, bool EnterprisePersistence)
{
    /// <summary>True only when all implemented adapters report their real prerequisites.</summary>
    public bool IsReady => Database && IdentityProvider && EnterprisePersistence;
}

/// <summary>Readiness from actual adapters, not a fixed HTTP response or an approval override.</summary>
public sealed class CheckApplicationReadiness(IDatabaseReadiness database, IIdentityProviderReadiness identity,
    IEnterpriseContextReader enterprise)
{
    /// <summary>Checks dependencies within a bounded budget, with cancellation.</summary>
    public async Task<ApplicationReadiness> ExecuteAsync(CancellationToken cancellationToken)
    {
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        budget.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            var db = database.CheckAsync(budget.Token);
            var provider = identity.CheckAsync(budget.Token);
            var persistence = enterprise.CheckProvisionedAsync(budget.Token);
            await Task.WhenAll(db, provider, persistence).WaitAsync(budget.Token);
            return new(await db, await provider, await persistence);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { return new(false, false, false); }
    }
}
