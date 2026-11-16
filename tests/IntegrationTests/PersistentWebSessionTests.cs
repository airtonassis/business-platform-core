using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Business.Platform.Core.Api.Web;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Npgsql;
using Xunit;

namespace Business.Platform.Core.IntegrationTests;

public sealed class SessionDatabaseFactAttribute:FactAttribute
{
    public SessionDatabaseFactAttribute() {if(Environment.GetEnvironmentVariable("BPC_P1A_PORT") is null)Skip="Requires authorized disposable PostgreSQL session harness.";}
}

public sealed class PersistentWebSessionTests
{
    private static string Root=>Environment.GetEnvironmentVariable("BPC_P1A_ROOT")!;
    private static string Password=>File.ReadAllText(Path.Combine(Root,"database-password")).Trim();
    private static PostgresOptions Options(int? port=null)=>new(){Host="127.0.0.1",Port=port??int.Parse(Environment.GetEnvironmentVariable("BPC_P1A_PORT")!),Name="bpc_sessions_local",Username="bpc_session_runtime",Password=Password};
    private static AuthenticationTicket Ticket(string sub="a")=>new(new ClaimsPrincipal(new ClaimsIdentity([new("iss","https://synthetic.invalid"),new("sub",sub)],"Web")),new AuthenticationProperties(),"Web");
    private static IDataProtectionProvider Protection(string app="local")=>DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Root,"keys")),b=>b.SetApplicationName("bpc:web:Development:"+app).ProtectKeysWithCertificate(Certificate()));
    private static X509Certificate2 Certificate()=>X509CertificateLoader.LoadPkcs12FromFile(Path.Combine(Root,"certificate.pfx"),File.ReadAllText(Path.Combine(Root,"certificate-password")).Trim(),X509KeyStorageFlags.EphemeralKeySet);
    private static async Task Admin(string sql,params object[] values)
    {
        var config=Options();var builder=new NpgsqlConnectionStringBuilder{Host=config.Host,Port=config.Port,Database=config.Name,Username="postgres",Password=File.ReadAllText(Path.Combine(Root,"admin-password")).Trim(),Pooling=false};
        await using var connection=new NpgsqlConnection(builder.ConnectionString);await connection.OpenAsync();
        await using var command=new NpgsqlCommand(sql,connection);foreach(var v in values)command.Parameters.AddWithValue(v);await command.ExecuteNonQueryAsync();
    }
    internal sealed class Host(int? port=null,bool backChannel=false,Action<IServiceCollection>? customize=null):WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
                ["Database:Host"]="localhost",["Database:Name"]="synthetic",["Database:Username"]="synthetic",["Database:Password"]="synthetic-only",
                ["WebAuthentication:Enabled"]="true",["WebAuthentication:Authority"]="https://synthetic.invalid",["WebAuthentication:ClientId"]="local",
                ["WebAuthentication:SessionStore"]="Postgres",["WebAuthentication:SessionDatabase:Host"]="127.0.0.1",
                ["WebAuthentication:BackChannelLogout:Enabled"]=backChannel.ToString(),
                ["WebAuthentication:SessionDatabase:Port"]=(port??Options().Port).ToString(),["WebAuthentication:SessionDatabase:Name"]="bpc_sessions_local",
                ["WebAuthentication:SessionDatabase:Username"]="bpc_session_runtime",["WebAuthentication:SessionDatabase:Password"]=Password,
                ["WebAuthentication:DataProtection:KeyRingPath"]=Path.Combine(Root,"keys"),["WebAuthentication:DataProtection:ApplicationName"]="local",
                ["WebAuthentication:DataProtection:CertificatePath"]=Path.Combine(Root,"certificate.pfx"),
                ["WebAuthentication:DataProtection:CertificatePasswordFile"]=Path.Combine(Root,"certificate-password"),["Logging:LogLevel:Default"]="Critical"
            }).Build();
            builder.ConfigureAppConfiguration((_,c)=>c.AddConfiguration(config));
            builder.ConfigureServices(services=>{
                if(!services.Any(x=>x.ServiceType==typeof(PostgresWebTicketStore)))
                    WebComposition.Add(services,config,new Microsoft.Extensions.Hosting.Internal.HostingEnvironment{EnvironmentName="Development"});
                services.RemoveAll<IEnterpriseContextReader>();services.AddSingleton<IEnterpriseContextReader>(new WebUiTests.Context());
                services.RemoveAll<IAuthenticatedIdentityBootstrap>();services.AddSingleton<IAuthenticatedIdentityBootstrap>(new WebUiTests.Context());
                services.AddSingleton<IStartupFilter,WebUiTests.FixtureSignIn>();
                customize?.Invoke(services);
            });
        }
        public HttpClient Browser()=>CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false,HandleCookies=false});
    }
    private static async Task<string> Cookie(HttpClient client)
    {
        var response=await client.GetAsync("/__fixture/owner");Assert.Equal(HttpStatusCode.NoContent,response.StatusCode);
        return string.Join("; ",response.Headers.GetValues("Set-Cookie").Where(v=>v.StartsWith("__Host-bpc-web",StringComparison.Ordinal)).Select(v=>v.Split(';')[0]));
    }
    private static async Task<HttpResponseMessage> Session(HttpClient client,string cookie,string path="/account/session")
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,path);request.Headers.Add("Cookie",cookie);return await client.SendAsync(request);
    }

    [SessionDatabaseFact]
    public async Task Cookie_survives_new_host_and_is_denied_by_bearer_API()
    {
        string cookie;await using(var host=new Host()){using var browser=host.Browser();cookie=await Cookie(browser);Assert.Equal(HttpStatusCode.OK,(await Session(browser,cookie)).StatusCode);}
        await using var next=new Host();using var client=next.Browser();Assert.Equal(HttpStatusCode.OK,(await Session(client,cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Session(client,cookie,"/api/v1/me/context")).StatusCode);
    }
    [SessionDatabaseFact]
    public async Task Revocation_in_second_instance_denies_copied_cookie()
    {
        await using var a=new Host();await using var b=new Host();using var browser=a.Browser();using var other=b.Browser();
        var cookie=await Cookie(browser);Assert.Equal(HttpStatusCode.OK,(await Session(other,cookie)).StatusCode);
        var store=a.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Web").SessionStore!;
        var format=a.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Web").TicketDataFormat;
        var handle=format.Unprotect(cookie.Split('=',2)[1])!.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;
        await b.Services.GetRequiredService<PostgresWebTicketStore>().RemoveAsync(handle);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Session(browser,cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await Session(other,cookie)).StatusCode);
    }
    [SessionDatabaseFact]
    public async Task Server_expiry_and_absolute_cap_ignore_ticket_expiry_and_reads_do_not_renew()
    {
        using var db=new PostgresSessionStore(Options());var store=new PostgresWebTicketStore(db,Protection());
        var key=await store.StoreAsync(Ticket());var before=(await db.RetrieveAsync(key))!.Value.Expires;
        Assert.NotNull(await store.RetrieveAsync(key));Assert.Equal(before,(await db.RetrieveAsync(key))!.Value.Expires);
        await Admin("UPDATE bpc_session.sessions SET idle_until=clock_timestamp()-interval '1 second' WHERE session_key=$1",key);
        Assert.Null(await store.RetrieveAsync(key));await store.RenewAsync(key,Ticket());Assert.Null(await store.RetrieveAsync(key));
        key=await store.StoreAsync(Ticket());await Admin("UPDATE bpc_session.sessions SET absolute_until=clock_timestamp()+interval '5 seconds',idle_until=clock_timestamp()+interval '4 seconds' WHERE session_key=$1",key);
        await store.RenewAsync(key,Ticket());var after=(await store.RetrieveAsync(key))!;Assert.True(after.Properties.ExpiresUtc<DateTimeOffset.UtcNow.AddSeconds(6));
        await Admin("UPDATE bpc_session.sessions SET absolute_until=clock_timestamp()-interval '1 second',idle_until=clock_timestamp()-interval '2 seconds' WHERE session_key=$1",key);
        await store.RenewAsync(key,Ticket());Assert.Null(await store.RetrieveAsync(key));
    }
    [SessionDatabaseFact]
    public async Task Concurrent_renew_remove_and_rotate_never_resurrect()
    {
        using var a=new PostgresSessionStore(Options());using var b=new PostgresSessionStore(Options());var first=new PostgresWebTicketStore(a,Protection());var second=new PostgresWebTicketStore(b,Protection());
        for(var i=0;i<12;i++) {
            var key=await first.StoreAsync(Ticket());var validating=first.RetrieveAsync(key);
            await Task.WhenAll(validating,first.RenewAsync(key,Ticket()),second.RemoveAsync(key));
            await second.RenewAsync(key,Ticket());Assert.Null(await first.RetrieveAsync(key));
        }
        var old=await first.StoreAsync(Ticket());var t=(await first.RetrieveAsync(old))!;
        var results=await Task.WhenAll(Enumerable.Range(0,2).Select(async _=>{try{return await second.StoreAsync(t);}catch(PostgresException e) when(e.SqlState=="P1701"){return null;}}));
        Assert.Single(results,x=>x is not null);Assert.Null(await first.RetrieveAsync(old));
        var changed=Ticket("other");var key2=await first.StoreAsync(Ticket());await second.RenewAsync(key2,changed);Assert.Null(await first.RetrieveAsync(key2));
    }
    [SessionDatabaseFact]
    public async Task Revocation_of_previous_handle_also_revokes_concurrently_rotated_session()
    {
        using var a=new PostgresSessionStore(Options());using var b=new PostgresSessionStore(Options());
        var first=new PostgresWebTicketStore(a,Protection());var second=new PostgresWebTicketStore(b,Protection());
        var old=await first.StoreAsync(Ticket());var authenticatedBeforeLogout=(await second.RetrieveAsync(old))!;
        var rotated=await second.StoreAsync(authenticatedBeforeLogout);
        await first.RemoveAsync(old);
        Assert.Null(await second.RetrieveAsync(rotated));
        await second.RenewAsync(rotated,Ticket());Assert.Null(await first.RetrieveAsync(rotated));
    }
    [SessionDatabaseFact]
    public async Task Failed_rotation_rolls_back_consumption_of_previous_session()
    {
        using var db=new PostgresSessionStore(Options());var store=new PostgresWebTicketStore(db,Protection());var key=await store.StoreAsync(Ticket());
        var identity=SHA256.HashData(System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new[]{"https://synthetic.invalid","a"}));
        var prior=(await store.RetrieveAsync(key))!;
        var error=await Assert.ThrowsAsync<PostgresException>(()=>db.StoreAsync(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),key,identity,new byte[65537],
            LogoutCorrelation.Hash("sub","https://synthetic.invalid","local","a"),null,null,long.Parse(prior.Principal.FindFirst("iat")!.Value)));
        Assert.Equal("23514",error.SqlState);Assert.NotNull(await store.RetrieveAsync(key));
    }
    [SessionDatabaseFact]
    public async Task Tickets_are_encrypted_bound_to_key_environment_and_certificate()
    {
        using var db=new PostgresSessionStore(Options());var store=new PostgresWebTicketStore(db,Protection());var key=await store.StoreAsync(Ticket());
        var ciphertext=(await db.RetrieveAsync(key))!.Value.Ticket;
        Assert.DoesNotContain("synthetic.invalid",System.Text.Encoding.UTF8.GetString(ciphertext));
        Assert.Null(await new PostgresWebTicketStore(db,Protection("different-environment")).RetrieveAsync(key));
        var other=await store.StoreAsync(Ticket());await Admin("UPDATE bpc_session.sessions SET protected_ticket=$1 WHERE session_key=$2",ciphertext,other);
        Assert.Null(await store.RetrieveAsync(other));
        using var rsa=RSA.Create(2048);using var wrong=new CertificateRequest("CN=wrong",rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1).CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(1));
        var wrongProvider=DataProtectionProvider.Create(new DirectoryInfo(Path.Combine(Root,"keys")),b=>b.SetApplicationName("bpc:web:Development:local").ProtectKeysWithCertificate(wrong));
        Assert.Null(await new PostgresWebTicketStore(db,wrongProvider).RetrieveAsync(key));
        Assert.All(Directory.GetFiles(Path.Combine(Root,"keys"),"key-*.xml"),p=>{var xml=File.ReadAllText(p);Assert.Contains("encryptedSecret",xml);Assert.DoesNotContain("<masterKey",xml);});
    }
    [SessionDatabaseFact]
    public async Task Store_failure_returns_sanitized_503_and_does_not_fallback()
    {
        string cookie;await using(var host=new Host()){using var client=host.Browser();cookie=await Cookie(client);}
        await using var unavailable=new Host(1);using var browser=unavailable.Browser();
        var response=await Session(browser,cookie);Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        var content=await response.Content.ReadAsStringAsync();Assert.DoesNotContain(Password,content);Assert.DoesNotContain("127.0.0.1",content);
        await using var recovered=new Host();using var next=recovered.Browser();Assert.Equal(HttpStatusCode.OK,(await Session(next,cookie)).StatusCode);
    }
    [SessionDatabaseFact]
    public async Task Process_phase_produces_or_reads_cookie_without_product_test_endpoint()
    {
        var phase=Environment.GetEnvironmentVariable("BPC_P1A_PHASE");if(phase is null)return;
        await using var host=new Host();using var browser=host.Browser();var file=Path.Combine(Root,"process-cookie");
        var revokedFile=Path.Combine(Root,"process-revoked-cookie");
        if(phase=="write") {
            await File.WriteAllTextAsync(file,await Cookie(browser));
            var revoked=await Cookie(browser);await File.WriteAllTextAsync(revokedFile,revoked);
            var format=host.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Web").TicketDataFormat;
            var handle=format.Unprotect(revoked.Split('=',2)[1])!.Principal.FindFirst("Microsoft.AspNetCore.Authentication.Cookies-SessionId")!.Value;
            await host.Services.GetRequiredService<PostgresWebTicketStore>().RemoveAsync(handle);
        } else {
            Assert.Equal(phase=="outage"?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK,(await Session(browser,await File.ReadAllTextAsync(file))).StatusCode);
            if(phase!="outage")Assert.Equal(HttpStatusCode.Unauthorized,(await Session(browser,await File.ReadAllTextAsync(revokedFile))).StatusCode);
        }
    }
}
