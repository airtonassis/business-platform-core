using Microsoft.Extensions.Logging;
using System.Net;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Shared;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using Xunit;
namespace Business.Platform.Core.IntegrationTests;
public sealed class ContextHttpTests
{
 private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> o, ILoggerFactory l, UrlEncoder e): AuthenticationHandler<AuthenticationSchemeOptions>(o,l,e)
 { protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("iss","https://synthetic.invalid"),new Claim("sub","A")},"synthetic")),Scheme.Name))); }
 private sealed class Reader(string state): IEnterpriseContextReader
 {
  public bool IsProvisioned=>state!="Pending";
  public Task<Result<EnterpriseContextSnapshot>> ReadAsync(OidcSubject s,CancellationToken t)
  {
   var id=Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
   var org=Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
   var acc=Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
   return Task.FromResult(Result.Success(new EnterpriseContextSnapshot(id,new(state=="Inconsistent"?BindingHistoryState.Unknown:state=="NotLinked"?BindingHistoryState.NeverConsumed:BindingHistoryState.Consumed,state=="NotLinked"?null:org),state=="NotLinked"?[]:[new(id,acc,org,acc,org,state!="Revoked",true,true)])));
  }
 }
 private sealed class Host(string state):WebApplicationFactory<Program>
 { protected override void ConfigureWebHost(IWebHostBuilder b)
   { b.ConfigureAppConfiguration((_,c)=>c.AddInMemoryCollection(new Dictionary<string,string?>{["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="bpc_test",["Database:Password"]="synthetic-only"}));
     b.ConfigureServices(s=>{s.RemoveAll<IEnterpriseContextReader>();s.AddSingleton<IEnterpriseContextReader>(new Reader(state));s.AddAuthentication(o=>{o.DefaultAuthenticateScheme="synthetic";o.DefaultChallengeScheme="synthetic";}).AddScheme<AuthenticationSchemeOptions,Auth>("synthetic",_=>{});}); }
 }
 [Theory][InlineData("Linked",200)][InlineData("NotLinked",200)][InlineData("Revoked",403)][InlineData("Inconsistent",500)][InlineData("Pending",503)]
 public async Task Approved_context_statuses(string state,int expected)
 { await using var h=new Host(state); using var c=h.CreateClient();var r=await c.GetAsync("/api/v1/me/context");Assert.Equal(expected,(int)r.StatusCode);var body=await r.Content.ReadAsStringAsync();if(expected==200)Assert.Contains(state,body);else Assert.DoesNotContain("bbbbbbbb",body);Assert.Contains("no-store",r.Headers.CacheControl!.ToString()); }
}


