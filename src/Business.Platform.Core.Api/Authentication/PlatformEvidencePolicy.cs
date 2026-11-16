using System.Globalization;
using System.Security.Claims;
namespace Business.Platform.Core.Api.Authentication;

/// <summary>Explicit server configuration for a separately verified IdP MFA profile. Unconfigured is deny.</summary>
public sealed class PlatformSecurityOptions
{
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string MfaProfile { get; set; } = "";
    public string RequiredAcr { get; set; } = "";
    public int MaxAuthenticationAgeSeconds { get; set; }
    public string CertificateAuthorityBase64 { get; set; } = "";
    public bool IsConfigured => Uri.TryCreate(Authority,UriKind.Absolute,out var uri) && uri.Scheme=="https" &&
        !string.IsNullOrWhiteSpace(Audience) && !string.IsNullOrWhiteSpace(ClientId) &&
        MfaProfile=="KeycloakRequiredOtpAcr2" && RequiredAcr=="2" && MaxAuthenticationAgeSeconds is >0 and <=300;
}

/// <summary>Additional authorization AFTER signature, issuer, audience, type and lifetime validation.
/// An acr claim alone is not proof: the configured Keycloak flow must separately be verified with real OTP.
/// No route/header/body input can supply this evidence. This policy does not register identities or access SQL.</summary>
public static class PlatformEvidencePolicy
{
    public static bool Allows(ClaimsPrincipal principal,PlatformSecurityOptions options,DateTimeOffset now)
    {
        if(!options.IsConfigured || principal.Identity?.IsAuthenticated!=true || principal.Identity.AuthenticationType!="Platform") return false;
        string? One(string name) { var claims=principal.FindAll(name).ToArray();return claims.Length==1&&!string.IsNullOrWhiteSpace(claims[0].Value)?claims[0].Value:null; }
        if(One("iss")!=options.Authority || One("sub") is null || One("client_id")!=options.ClientId || One("acr")!=options.RequiredAcr ||
            !principal.FindAll("platform_roles").Any(c=>c.Value=="PlatformOperator")) return false;
        var methods=principal.FindAll("amr").Select(c=>c.Value).ToHashSet(StringComparer.Ordinal);
        if(!methods.Contains("pwd")||!methods.Contains("otp")) return false;
        if(!long.TryParse(One("auth_time"),NumberStyles.None,CultureInfo.InvariantCulture,out var time)) return false;
        var seconds=now.ToUnixTimeSeconds();
        return time<=seconds && time>=seconds-options.MaxAuthenticationAgeSeconds;
    }
}
