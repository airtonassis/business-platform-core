using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Business.Platform.Core.Api.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class BackChannelLogoutTests
{
    private const string Issuer="https://synthetic.invalid";
    private static readonly RSA Rsa=RSA.Create(2048);
    private static readonly RsaSecurityKey Key=new(Rsa){KeyId="fixture-key-1"};
    private static AuthenticationTicket Ticket(string sub="owner",string? sid="session-a",string issuer=Issuer,long? issued=null)
    {
        var claims=new List<Claim>{new("iss",issuer),new("sub",sub),new("iat",(issued??DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString())};
        if(sid is not null)claims.Add(new("sid",sid));
        return new(new ClaimsPrincipal(new ClaimsIdentity(claims,"Web")),new AuthenticationProperties(),"Web");
    }
    private static Dictionary<string,object?> Payload(string? sub="owner",string? sid="session-a")
    {
        var now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var d=new Dictionary<string,object?>{["iss"]=Issuer,["aud"]="local",["iat"]=now,["exp"]=now+60,["jti"]=Guid.NewGuid().ToString(),["events"]=new Dictionary<string,object>{{"http://schemas.openid.net/event/backchannel-logout",new{}}}};
        if(sub is not null)d["sub"]=sub;if(sid is not null)d["sid"]=sid;return d;
    }
    private static string Token(Dictionary<string,object?> p,RSA? rsa=null,string kid="fixture-key-1")
    {
        var h=Base64UrlEncoder.Encode(JsonSerializer.Serialize(new{alg="RS256",typ="logout+jwt",kid}));var b=Base64UrlEncoder.Encode(JsonSerializer.Serialize(p));
        return h+"."+b+"."+Base64UrlEncoder.Encode((rsa??Rsa).SignData(Encoding.ASCII.GetBytes(h+"."+b),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
    }
    private static PersistentWebSessionTests.Host Host(int? port=null,OpenIdConnectConfiguration? configuration=null)
    {
        configuration??=new OpenIdConnectConfiguration{Issuer=Issuer};if(configuration.SigningKeys.Count==0)configuration.SigningKeys.Add(Key);
        return new(port,true,services=>services.PostConfigure<OpenIdConnectOptions>("WebOidc",o=>o.ConfigurationManager=new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration)));
    }
    private static Task<HttpResponseMessage> Send(HttpClient browser,string token)=>browser.PostAsync(BackChannelLogout.Path,new FormUrlEncodedContent(new Dictionary<string,string>{{"logout_token",token}}));

    [SessionDatabaseFact]
    public async Task Valid_sid_logout_is_scoped_idempotent_and_persistent_across_hosts()
    {
        await using var host=Host();using var browser=host.Browser();var store=host.Services.GetRequiredService<PostgresWebTicketStore>();
        var a=await store.StoreAsync(Ticket());var second=await store.StoreAsync(Ticket());
        var otherSession=await store.StoreAsync(Ticket(sid:"other"));var b=await store.StoreAsync(Ticket("other"));var issuer=await store.StoreAsync(Ticket(issuer:"https://other.invalid"));
        var p=Payload();var token=Token(p);Assert.Equal(HttpStatusCode.OK,(await Send(browser,token)).StatusCode);
        Assert.Null(await store.RetrieveAsync(a));Assert.Null(await store.RetrieveAsync(second));
        Assert.NotNull(await store.RetrieveAsync(otherSession));Assert.NotNull(await store.RetrieveAsync(b));Assert.NotNull(await store.RetrieveAsync(issuer));
        await using var next=Host();using var client=next.Browser();Assert.Equal(HttpStatusCode.OK,(await Send(client,token)).StatusCode);
        Assert.Null(await next.Services.GetRequiredService<PostgresWebTicketStore>().RetrieveAsync(a));
        p["sub"]="other";Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,Token(p))).StatusCode);Assert.NotNull(await store.RetrieveAsync(b));
    }
    [SessionDatabaseFact]
    public async Task Subject_only_revokes_all_prior_sessions_but_not_new_login()
    {
        await using var host=Host();using var browser=host.Browser();var store=host.Services.GetRequiredService<PostgresWebTicketStore>();var sub=Guid.NewGuid().ToString();
        var before=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-2;
        var a=await store.StoreAsync(Ticket(sub,"one",issued:before));var b=await store.StoreAsync(Ticket(sub,"two",issued:before));var p=Payload(sub,null);
        Assert.Equal(HttpStatusCode.OK,(await Send(browser,Token(p))).StatusCode);Assert.Null(await store.RetrieveAsync(a));Assert.Null(await store.RetrieveAsync(b));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>store.StoreAsync(Ticket(sub,"late-old",issued:before)));
        var future=await store.StoreAsync(Ticket(sub,"new",issued:(long)p["iat"]!+1));Assert.NotNull(await store.RetrieveAsync(future));
    }
    [SessionDatabaseFact]
    public async Task Sid_only_and_mismatched_pair_do_not_revoke_other_identity()
    {
        await using var host=Host();using var client=host.Browser();var store=host.Services.GetRequiredService<PostgresWebTicketStore>();
        var sid=Guid.NewGuid().ToString();var a=await store.StoreAsync(Ticket(sid:sid));
        Assert.Equal(HttpStatusCode.OK,(await Send(client,Token(Payload("wrong",sid)))).StatusCode);Assert.NotNull(await store.RetrieveAsync(a));
        Assert.Equal(HttpStatusCode.OK,(await Send(client,Token(Payload(null,sid)))).StatusCode);Assert.Null(await store.RetrieveAsync(a));
        await Assert.ThrowsAsync<Npgsql.PostgresException>(()=>store.StoreAsync(Ticket(sid:sid)));
    }
    [SessionDatabaseFact]
    public async Task Invalid_tokens_never_revoke_and_signing_key_rotation_is_supported()
    {
        var metadata=new OpenIdConnectConfiguration{Issuer=Issuer};metadata.SigningKeys.Add(Key);
        await using var host=Host(configuration:metadata);using var client=host.Browser();var store=host.Services.GetRequiredService<PostgresWebTicketStore>();
        var sid=Guid.NewGuid().ToString();var key=await store.StoreAsync(Ticket(sid:sid));
        var mutations=new Action<Dictionary<string,object?>>[]{p=>p["iss"]="https://wrong.invalid",p=>p["aud"]="wrong",p=>p["aud"]="local/",p=>p["nonce"]="forbidden",p=>p["iat"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-200,
            p=>p["iat"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds()+30,p=>p["exp"]=DateTimeOffset.UtcNow.ToUnixTimeSeconds()-30,p=>p.Remove("jti"),p=>p.Remove("exp"),p=>p["events"]=new{},p=>{p.Remove("sid");p.Remove("sub");}};
        foreach(var mutate in mutations){var p=Payload(sid:sid);mutate(p);Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,Token(p))).StatusCode);Assert.NotNull(await store.RetrieveAsync(key));}
        using var newRsa=RSA.Create(2048);Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,Token(Payload(sid:sid),newRsa,"new-key"))).StatusCode);
        metadata.SigningKeys.Add(new RsaSecurityKey(newRsa){KeyId="new-key"});
        Assert.Equal(HttpStatusCode.OK,(await Send(client,Token(Payload(sid:sid),newRsa,"new-key"))).StatusCode);Assert.Null(await store.RetrieveAsync(key));
    }
    [SessionDatabaseFact]
    public async Task Concurrent_logout_rotation_renewal_and_replay_never_resurrect()
    {
        await using var a=Host();await using var b=Host();using var client=a.Browser();using var other=b.Browser();
        var first=a.Services.GetRequiredService<PostgresWebTicketStore>();var second=b.Services.GetRequiredService<PostgresWebTicketStore>();
        var sid=Guid.NewGuid().ToString();var key=await first.StoreAsync(Ticket(sid:sid));var ticket=(await first.RetrieveAsync(key))!;var token=Token(Payload(sid:sid));
        string? rotated=null;var rotation=Task.Run(async()=>{try{rotated=await second.StoreAsync(ticket);}catch(Npgsql.PostgresException e) when(e.SqlState=="P1701") {}});
        await Task.WhenAll(rotation,first.RenewAsync(key,ticket),Task.Run(async()=>Assert.Equal(HttpStatusCode.OK,(await Send(client,token)).StatusCode)),Task.Run(async()=>Assert.Equal(HttpStatusCode.OK,(await Send(other,token)).StatusCode)));
        Assert.Null(await first.RetrieveAsync(key));if(rotated is not null)Assert.Null(await second.RetrieveAsync(rotated));
    }
    [SessionDatabaseFact]
    public async Task Input_limits_and_store_failure_are_safe_without_cookie_authentication()
    {
        await using var host=Host();using var client=host.Browser();
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(client,"malformed")).StatusCode);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType,(await client.PostAsync(BackChannelLogout.Path,new StringContent("{}",Encoding.UTF8,"application/json"))).StatusCode);
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge,(await Send(client,new string('A',17000))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync(BackChannelLogout.Path,new StringContent("logout_token=a&logout_token=b",Encoding.UTF8,"application/x-www-form-urlencoded"))).StatusCode);
        await using var unavailable=Host(1);using var down=unavailable.Browser();down.DefaultRequestHeaders.Add("Cookie","__Host-bpc-web=untrusted");
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(down,"invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Send(down,Token(Payload()))).StatusCode);
        var options=host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get("Web");
        var ticket=new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity([new("Microsoft.AspNetCore.Authentication.Cookies-SessionId",new string('A',64))],"Web")),"Web");
        down.DefaultRequestHeaders.Remove("Cookie");down.DefaultRequestHeaders.Add("Cookie","__Host-bpc-web="+options.TicketDataFormat.Protect(ticket));
        Assert.Equal(HttpStatusCode.BadRequest,(await down.PostAsync(BackChannelLogout.Path+"/",new FormUrlEncodedContent(new Dictionary<string,string>{{"logout_token","invalid"}}))).StatusCode);
    }
    [SessionDatabaseFact]
    public async Task Metadata_refresh_validates_new_key_and_unavailable_metadata_returns_503()
    {
        using var nextKey=RSA.Create(2048);var old=new OpenIdConnectConfiguration{Issuer=Issuer};old.SigningKeys.Add(Key);
        var next=new OpenIdConnectConfiguration{Issuer=Issuer};next.SigningKeys.Add(new RsaSecurityKey(nextKey){KeyId="refreshed"});
        var manager=new RefreshingMetadata(old,next);
        await using var host=new PersistentWebSessionTests.Host(null,true,s=>s.PostConfigure<OpenIdConnectOptions>("WebOidc",o=>o.ConfigurationManager=manager));
        using var browser=host.Browser();var token=Token(Payload(sid:Guid.NewGuid().ToString()),nextKey,"refreshed");
        Assert.Equal(HttpStatusCode.OK,(await Send(browser,token)).StatusCode);Assert.True(manager.Refreshed);
        Assert.Equal(HttpStatusCode.OK,(await Send(browser,token)).StatusCode);
        manager.Unavailable=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await Send(browser,Token(Payload()))).StatusCode);
    }
    private sealed class RefreshingMetadata(OpenIdConnectConfiguration initial,OpenIdConnectConfiguration next):IConfigurationManager<OpenIdConnectConfiguration>
    {
        public bool Refreshed,Unavailable;
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel)=>Unavailable?throw new InvalidOperationException("Fixture unavailable"):Task.FromResult(Refreshed?next:initial);
        public void RequestRefresh()=>Refreshed=true;
    }
    [Fact]
    public async Task Validator_retries_new_signing_key_in_same_request_after_metadata_refresh()
    {
        using var nextKey=RSA.Create(2048);var old=new OpenIdConnectConfiguration{Issuer=Issuer};old.SigningKeys.Add(Key);
        var next=new OpenIdConnectConfiguration{Issuer=Issuer};next.SigningKeys.Add(new RsaSecurityKey(nextKey){KeyId="refreshed"});
        var manager=new RefreshingMetadata(old,next);var services=new ServiceCollection();
        services.AddOptions<OpenIdConnectOptions>("WebOidc").Configure(o=>{o.Authority=Issuer;o.ClientId="local";o.ConfigurationManager=manager;});
        await using var provider=services.BuildServiceProvider();
        var validator=new LogoutTokenValidator(provider.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenIdConnectOptions>>(),TimeProvider.System);
        Assert.NotNull(await validator.ValidateAsync(Token(Payload(),nextKey,"refreshed"),CancellationToken.None));
        Assert.True(manager.Refreshed);
    }
    [SessionDatabaseFact]
    public async Task Nonce_null_bad_signature_duplicate_properties_and_rate_limit_are_rejected()
    {
        await using var host=Host();using var browser=host.Browser();var p=Payload(sid:Guid.NewGuid().ToString());p["nonce"]=null;
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(browser,Token(p))).StatusCode);
        using var wrong=RSA.Create(2048);Assert.Equal(HttpStatusCode.BadRequest,(await Send(browser,Token(Payload(),wrong))).StatusCode);
        var header=Base64UrlEncoder.Encode("{\"alg\":\"RS256\",\"typ\":\"JWT\"}");
        var duplicate=Base64UrlEncoder.Encode("{\"iss\":\"https://synthetic.invalid\",\"iss\":\"https://synthetic.invalid\"}");
        var input=header+"."+duplicate;var raw=input+"."+Base64UrlEncoder.Encode(Rsa.SignData(Encoding.ASCII.GetBytes(input),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1));
        Assert.Equal(HttpStatusCode.BadRequest,(await Send(browser,raw)).StatusCode);
        for(var i=0;i<27;i++)Assert.Equal(HttpStatusCode.BadRequest,(await Send(browser,"invalid")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await Send(browser,"invalid")).StatusCode);
    }
    [SessionDatabaseFact]
    public async Task Enabled_profile_cannot_issue_or_read_uncorrelated_legacy_ticket()
    {
        await using var host=Host();using var browser=host.Browser();var strict=host.Services.GetRequiredService<PostgresWebTicketStore>();
        var missing=Ticket(sid:null);await Assert.ThrowsAsync<InvalidOperationException>(()=>strict.StoreAsync(missing));
        var legacy=new PostgresWebTicketStore(host.Services.GetRequiredService<Business.Platform.Core.Infrastructure.Persistence.PostgresSessionStore>(),host.Services.GetRequiredService<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>());
        var key=await legacy.StoreAsync(missing);Assert.NotNull(await legacy.RetrieveAsync(key));Assert.Null(await strict.RetrieveAsync(key));
    }
    [SessionDatabaseFact]
    public async Task Process_phase_persists_revocation_and_replay_between_processes()
    {
        var phase=Environment.GetEnvironmentVariable("BPC_P1B_PHASE");if(phase is null)return;
        var root=Environment.GetEnvironmentVariable("BPC_P1A_ROOT")!;
        using var rsa=RSA.Create();rsa.ImportPkcs8PrivateKey(await File.ReadAllBytesAsync(Path.Combine(root,"signing.key")),out _);
        var metadata=new OpenIdConnectConfiguration{Issuer=Issuer};metadata.SigningKeys.Add(new RsaSecurityKey(rsa){KeyId="process-key"});
        await using var host=Host(configuration:metadata);using var browser=host.Browser();var store=host.Services.GetRequiredService<PostgresWebTicketStore>();
        if(phase=="create") {
            await File.WriteAllTextAsync(Path.Combine(root,"session-key"),await store.StoreAsync(Ticket(sid:"process-sid")));
            await File.WriteAllTextAsync(Path.Combine(root,"logout-token"),Token(Payload(sid:"process-sid"),rsa,"process-key"));
        } else {
            var key=await File.ReadAllTextAsync(Path.Combine(root,"session-key"));var token=await File.ReadAllTextAsync(Path.Combine(root,"logout-token"));
            Assert.Equal(phase=="outage"?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK,(await Send(browser,token)).StatusCode);
            if(phase!="outage")Assert.Null(await store.RetrieveAsync(key));
        }
    }
}
