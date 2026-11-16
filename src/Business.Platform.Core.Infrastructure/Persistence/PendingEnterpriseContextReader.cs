using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;

namespace Business.Platform.Core.Infrastructure.Persistence;

// Intentionally no SQL/migrations: the physical contract remains proposed.
public sealed class PendingEnterpriseContextReader : IEnterpriseContextReader
{
    public bool IsProvisioned => false;
    public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject subject, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Result.Failure<EnterpriseContextSnapshot>(new Error("Enterprise.PersistencePending",
            "Enterprise persistence is not provisioned.")));
    }
}
