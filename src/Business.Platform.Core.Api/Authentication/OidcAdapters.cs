using Business.Platform.Core.Application.Operations;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;

namespace Business.Platform.Core.Api.Authentication;

public sealed class HttpAuthenticatedSubject(IHttpContextAccessor accessor) : IAuthenticatedSubject
{
    public OidcSubject? GetSubject()
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return null;
        var issuer = user.FindAll("iss").ToArray();
        var subject = user.FindAll("sub").ToArray();
        return issuer.Length == 1 && subject.Length == 1 && !string.IsNullOrWhiteSpace(issuer[0].Value) &&
            !string.IsNullOrWhiteSpace(subject[0].Value) ? new(issuer[0].Value, subject[0].Value) : null;
    }
}

public sealed class OidcProviderReadiness(IOptionsMonitor<JwtBearerOptions> options) : IIdentityProviderReadiness
{
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        var configured = options.Get(JwtBearerDefaults.AuthenticationScheme);
        if (string.IsNullOrWhiteSpace(configured.Authority) || string.IsNullOrWhiteSpace(configured.Audience) ||
            configured.ConfigurationManager is null) return false;
        try
        {
            var metadata = await configured.ConfigurationManager.GetConfigurationAsync(cancellationToken);
            return metadata.Issuer == configured.Authority && metadata.SigningKeys.Count > 0;
        }
        catch (HttpRequestException) { return false; }
        catch (InvalidOperationException) { return false; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }
}
