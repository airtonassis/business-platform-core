using System.Security.Claims;
using Business.Platform.Core.Api.Authentication;
using Xunit;
namespace Business.Platform.Core.IntegrationTests;
public sealed class PlatformEvidenceTests
{
 private static readonly DateTimeOffset Now=DateTimeOffset.FromUnixTimeSeconds(1800000000);
 private static PlatformSecurityOptions Config()=>new(){Authority="https://idp.example/realms/core",Audience="core-platform",ClientId="platform-web",MfaProfile="KeycloakRequiredOtpAcr2",RequiredAcr="2",MaxAuthenticationAgeSeconds=300};
 private static ClaimsPrincipal Principal(string fault="")
 {
  var claims=new List<Claim>{new("iss","https://idp.example/realms/core"),new("sub","operator"),new("client_id","platform-web"),new("platform_roles","PlatformOperator"),new("acr","2"),new("amr","pwd"),new("amr","otp"),new("auth_time",Now.AddSeconds(-30).ToUnixTimeSeconds().ToString())};
  if(fault=="methods")claims.RemoveAll(c=>c.Type=="amr");
  if(fault=="role")claims.RemoveAll(c=>c.Type=="platform_roles");
  if(fault=="mfa")claims.RemoveAll(c=>c.Type=="acr");
  if(fault=="stale") {claims.RemoveAll(c=>c.Type=="auth_time");claims.Add(new("auth_time",Now.AddSeconds(-301).ToUnixTimeSeconds().ToString()));}
  if(fault=="future") {claims.RemoveAll(c=>c.Type=="auth_time");claims.Add(new("auth_time",Now.AddSeconds(60).ToUnixTimeSeconds().ToString()));}
  if(fault=="duplicate")claims.Add(new("acr","2"));
  if(fault=="client") {claims.RemoveAll(c=>c.Type=="client_id");claims.Add(new("client_id","tenant-web"));}
  return new(new ClaimsIdentity(claims,fault=="scheme"?"Bearer":"Platform"));
 }
 [Theory][InlineData("role")][InlineData("mfa")][InlineData("stale")][InlineData("future")][InlineData("duplicate")][InlineData("client")][InlineData("scheme")][InlineData("methods")]
 public void Rejects_insufficient_or_wrong_scheme_evidence(string fault)=>Assert.False(PlatformEvidencePolicy.Allows(Principal(fault),Config(),Now));
 [Fact] public void Unconfigured_contract_fails_closed()=>Assert.False(PlatformEvidencePolicy.Allows(Principal(),new(),Now));
 [Fact] public void Validated_scheme_and_configured_evidence_are_required_together()=>Assert.True(PlatformEvidencePolicy.Allows(Principal(),Config(),Now));
}
