using Business.Platform.Core.Domain.Tenancy;
namespace Business.Platform.Core.Application.Tenancy;
/// <summary>Authentication lifecycle operation, separate from a side-effect-free context GET.</summary>
public interface IAuthenticatedIdentityBootstrap
{
    /// <summary>Registers only the exact subject of an already validated access token.</summary>
    Task RegisterAsync(OidcSubject subject, CancellationToken cancellationToken);
}
