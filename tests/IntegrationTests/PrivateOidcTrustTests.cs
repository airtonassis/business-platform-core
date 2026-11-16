using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Business.Platform.Core.Api.Authentication;
using Xunit;
namespace Business.Platform.Core.IntegrationTests;
public sealed class PrivateOidcTrustTests
{
 [Fact] public void Client_authentication_only_certificate_is_not_a_TLS_server_certificate()
 {
  using var rootKey=RSA.Create(2048);
  var rootRequest=new CertificateRequest("CN=private-root",rootKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
  using var root=rootRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-2),DateTimeOffset.UtcNow.AddDays(1));
  using var leafKey=RSA.Create(2048);
  var leafRequest=new CertificateRequest("CN=synthetic-server",leafKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  leafRequest.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {new Oid("1.3.6.1.5.5.7.3.2")},true));
  using var leaf=leafRequest.Create(root,DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddHours(1),new byte[]{1,2,3});
  Assert.False(PrivateOidcTrust.Validate(root,leaf,SslPolicyErrors.RemoteCertificateChainErrors));
 }
 [Fact] public void Private_CA_does_not_disable_hostname_or_chain_validation()
 {
  using var key=RSA.Create(2048);var req=new CertificateRequest("CN=synthetic",key,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  req.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
  using var ca=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
  Assert.True(PrivateOidcTrust.Validate(ca,ca,SslPolicyErrors.RemoteCertificateChainErrors));
  Assert.False(PrivateOidcTrust.Validate(ca,ca,SslPolicyErrors.RemoteCertificateNameMismatch));
  using var otherKey=RSA.Create(2048);var otherReq=new CertificateRequest("CN=other",otherKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
  using var other=otherReq.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(1));
  Assert.False(PrivateOidcTrust.Validate(ca,other,SslPolicyErrors.RemoteCertificateChainErrors));
  Assert.False(PrivateOidcTrust.Validate(ca,null,SslPolicyErrors.RemoteCertificateNotAvailable));
 }
}
