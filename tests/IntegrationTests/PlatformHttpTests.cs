using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using Business.Platform.Core.Api.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Business.Platform.Core.Application.Platform;
using Business.Platform.Core.Domain.Shared;
using System.Net.Http.Json;
namespace Business.Platform.Core.IntegrationTests;
public sealed class PlatformHttpTests
{
 internal sealed class Host(IPlatformStore? realStore = null):WebApplicationFactory<Program>
 {
  public sealed class Spy:IAuthenticatedIdentityBootstrap { public int Calls; public Task RegisterAsync(OidcSubject s,CancellationToken c) {Calls++;return Task.CompletedTask;} }
  public Spy Bootstrap {get;}=new();
  public sealed class Store:IPlatformStore {
   public int Reads,Writes;
   public Task<Result<RecipientReferenceIssued>> IssueAsync(OidcSubject s,byte[] h,string n,CancellationToken c) => Task.FromResult(Result.Success(new RecipientReferenceIssued(Guid.NewGuid(),DateTimeOffset.UtcNow.AddHours(24))));
   public Task<Result<OwnerPreview>> PreviewAsync(Guid u,byte[] h,CancellationToken c) {Reads++;return Task.FromResult(Result.Success(new OwnerPreview(u,"Recipient")));}
   public Task<Result<PlatformCreation>> CreateAsync(OidcSubject s,Guid u,byte[] h,string n,Guid o,CancellationToken c) {Writes++;return Task.FromResult(Result.Success(new PlatformCreation(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),false)));}
  }
  public Store Persistence {get;}=new();
  public RSA Key {get;}=RSA.Create(2048);
  protected override void ConfigureWebHost(IWebHostBuilder b)
  {
   b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{
    ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="bpc_test",["Database:Password"]="synthetic-only",
    ["PlatformAuthentication:Authority"]="https://idp.example/realms/core",["PlatformAuthentication:Audience"]="core-platform",["PlatformAuthentication:ClientId"]="platform-web",
    ["PlatformAuthentication:MfaProfile"]="KeycloakRequiredOtpAcr2",["PlatformAuthentication:RequiredAcr"]="2",["PlatformAuthentication:MaxAuthenticationAgeSeconds"]="300"}));
   b.ConfigureServices(s=>{
    s.RemoveAll<IAuthenticatedIdentityBootstrap>();s.AddSingleton<IAuthenticatedIdentityBootstrap>(Bootstrap);
    s.RemoveAll<IPlatformStore>();s.AddSingleton<IPlatformStore>(realStore ?? Persistence);
    s.PostConfigure<JwtBearerOptions>("Bearer",o=>{
     o.Authority="https://idp.example/realms/core";o.Audience="core-api";
     o.TokenValidationParameters.ValidIssuer=o.Authority;o.TokenValidationParameters.ValidAudience=o.Audience;
     var meta=new OpenIdConnectConfiguration {Issuer=o.Authority};meta.SigningKeys.Add(new RsaSecurityKey(Key.ExportParameters(false)){KeyId="synthetic-platform"});
     o.ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(meta);
    });
    s.PostConfigure<JwtBearerOptions>("Platform",o=>{
    var meta=new OpenIdConnectConfiguration{Issuer="https://idp.example/realms/core"};meta.SigningKeys.Add(new RsaSecurityKey(Key.ExportParameters(false)){KeyId="synthetic-platform"});
    o.ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(meta);
   }); });
  }
  public string Token(string fault,string subject="operator")
  {
   var now=DateTimeOffset.UtcNow;
   var claims=new List<Claim>{new("sub",subject),new("client_id",fault=="tenant"?"tenant-web":"platform-web"),new("jti",Guid.NewGuid().ToString()),new("iat",now.ToUnixTimeSeconds().ToString(),ClaimValueTypes.Integer64),
    new("auth_time",now.AddSeconds(fault=="stale"?-301:-20).ToUnixTimeSeconds().ToString(),ClaimValueTypes.Integer64),new("acr","2"),new("amr","pwd"),new("amr","otp"),new("platform_roles","PlatformOperator")};
   if(fault=="no-mfa")claims.RemoveAll(c=>c.Type is "acr" or "amr");
   if(fault=="no-role")claims.RemoveAll(c=>c.Type=="platform_roles");
   if(fault=="missing-sub")claims.RemoveAll(c=>c.Type=="sub");
   var signing=new SigningCredentials(new RsaSecurityKey(Key){KeyId="synthetic-platform"},SecurityAlgorithms.RsaSha256);
   var jwt=new JwtSecurityToken(new JwtHeader(signing){["typ"]="at+jwt"},new JwtPayload("https://idp.example/realms/core",fault=="tenant"?"core-api":"core-platform",claims,now.AddMinutes(-10).UtcDateTime,now.AddMinutes(fault=="expired"?-5:5).UtcDateTime));
   var token=new JwtSecurityTokenHandler().WriteToken(jwt);return fault=="signature"?token[..^10]+"AAAAAAAAAA":token;
  }
 }
 [Theory][InlineData("tenant",401)][InlineData("expired",401)][InlineData("signature",401)][InlineData("missing-sub",401)][InlineData("no-role",403)][InlineData("no-mfa",403)][InlineData("stale",403)]
 public async Task Platform_routes_require_own_validated_scheme_before_any_persistence(string fault,int expected)
 {
  await using var h=new Host();using var client=h.CreateClient();client.DefaultRequestHeaders.Authorization=new("Bearer",h.Token(fault));
  client.DefaultRequestHeaders.Add("X-Platform-Role","PlatformOperator");client.DefaultRequestHeaders.Add("X-MFA","true");
  foreach(var path in new[]{"/api/v1/platform/owner-preview","/api/v1/platform/organizations"}) {
   var response=await client.PostAsJsonAsync(path,new {});
   var body=await response.Content.ReadAsStringAsync();Assert.True(expected==(int)response.StatusCode,$"Expected {expected}, got {(int)response.StatusCode}: {body}");Assert.DoesNotContain("synthetic-only",body);Assert.DoesNotContain("operator",body);
  }
  Assert.Equal(0,h.Bootstrap.Calls);
  Assert.Equal(0,h.Persistence.Reads+h.Persistence.Writes);
  h.Key.Dispose();
 }
 [Fact] public async Task Anonymous_cannot_execute_either_operation()
 {await using var h=new Host();using var c=h.CreateClient();Assert.Equal(HttpStatusCode.Unauthorized,(await c.PostAsync("/api/v1/platform/organizations",new StringContent("{}"))).StatusCode);h.Key.Dispose();}
 [Fact] public async Task Platform_access_token_cannot_authenticate_to_tenant_context()
 {
  await using var h=new Host();using var c=h.CreateClient();c.DefaultRequestHeaders.Authorization=new("Bearer",h.Token("valid"));
  Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/context")).StatusCode);
  Assert.Equal(HttpStatusCode.Unauthorized,(await c.GetAsync("/api/v1/organizations/aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa/audit-events")).StatusCode);
  Assert.Equal(0,h.Bootstrap.Calls);Assert.Equal(0,h.Persistence.Reads+h.Persistence.Writes);h.Key.Dispose();
 }
 [Fact] public async Task Valid_operator_previews_without_writes_and_creates_with_explicit_key()
 {
  await using var h=new Host();using var c=h.CreateClient();c.DefaultRequestHeaders.Authorization=new("Bearer",h.Token("valid"));
  var dto=new {userId=Guid.NewGuid(),recipientReference=new string('A',43),name="Synthetic"};
  Assert.Equal(HttpStatusCode.OK,(await c.PostAsJsonAsync("/api/v1/platform/owner-preview",dto)).StatusCode);
  Assert.Equal(0,h.Persistence.Writes);Assert.Equal(0,h.Bootstrap.Calls);
  Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v1/platform/organizations",dto)).StatusCode);
  c.DefaultRequestHeaders.Add("Idempotency-Key","synthetic-operation");
  Assert.Equal(HttpStatusCode.Created,(await c.PostAsJsonAsync("/api/v1/platform/organizations",dto)).StatusCode);
  Assert.Equal(1,h.Persistence.Writes);h.Key.Dispose();
 }
 [Fact] public async Task Invalid_reference_is_uniform_not_found_without_store_call()
 {
  await using var h=new Host();using var c=h.CreateClient();c.DefaultRequestHeaders.Authorization=new("Bearer",h.Token("valid"));
  Assert.Equal(HttpStatusCode.NotFound,(await c.PostAsJsonAsync("/api/v1/platform/owner-preview",new {userId=Guid.NewGuid(),recipientReference="invalid"})).StatusCode);
  Assert.Equal(0,h.Persistence.Reads);h.Key.Dispose();
 }
 [Fact] public async Task Preview_enforces_ten_per_operator_without_trusting_client_ip_headers()
 {
  await using var h=new Host();using var c=h.CreateClient();c.DefaultRequestHeaders.Authorization=new("Bearer",h.Token("valid"));
  for(var i=0;i<11;i++) {
   c.DefaultRequestHeaders.Remove("X-Forwarded-For");c.DefaultRequestHeaders.Add("X-Forwarded-For","192.0.2."+i);
   var path=i%2==0?"/api/v1/platform/owner-preview/":"/api/v1/PLATFORM/OWNER-PREVIEW";
   var r=await c.PostAsJsonAsync(path,new {userId=Guid.NewGuid(),recipientReference=new string('A',43)});
   Assert.Equal(i<10?200:429,(int)r.StatusCode);
  }
  Assert.Equal(10,h.Persistence.Reads);Assert.Equal(0,h.Persistence.Writes);h.Key.Dispose();
 }
 [Fact] public async Task Preview_enforces_thirty_per_ip_across_different_operators()
 {
  await using var h=new Host();using var c=h.CreateClient();
  for(var i=0;i<31;i++) {
   c.DefaultRequestHeaders.Authorization=new("Bearer",h.Token("valid","operator-"+i));
   c.DefaultRequestHeaders.Remove("X-Forwarded-For");c.DefaultRequestHeaders.Add("X-Forwarded-For","192.0.2."+i);
   var r=await c.PostAsJsonAsync("/api/v1/platform/owner-preview",new {userId=Guid.NewGuid(),recipientReference=new string('A',43)});
   Assert.Equal(i<30?200:429,(int)r.StatusCode);
  }
  Assert.Equal(30,h.Persistence.Reads);Assert.Equal(0,h.Persistence.Writes);h.Key.Dispose();
 }
}
