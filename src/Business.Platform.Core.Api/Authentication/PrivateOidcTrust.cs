using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
namespace Business.Platform.Core.Api.Authentication;

/// <summary>Trusts an operator-configured private CA while retaining TLS hostname and chain checks.</summary>
public static class PrivateOidcTrust
{
    public static bool Validate(X509Certificate2 authority, X509Certificate2? certificate, SslPolicyErrors errors)
    {
        if (certificate is null || (errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None) return false;
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(authority);
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid("1.3.6.1.5.5.7.3.1"));
        // This private environment has no CRL endpoint; validity and issuer chain remain mandatory.
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        return chain.Build(certificate);
    }
    public static HttpClientHandler Handler(string base64Certificate)
    {
        var authority = X509CertificateLoader.LoadCertificate(Convert.FromBase64String(base64Certificate));
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (_, certificate, _, errors) => Validate(authority,certificate,errors);
        return handler;
    }
}
