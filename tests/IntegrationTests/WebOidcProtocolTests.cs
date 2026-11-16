using System.Net;
using System.Text.RegularExpressions;
using Xunit;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Business.Platform.Core.IntegrationTests;

public sealed class LocalOidcFactAttribute:FactAttribute
{
    public LocalOidcFactAttribute(){if(Environment.GetEnvironmentVariable("BPC_E205_OIDC_AUTHORITY") is null)Skip="Requires guarded disposable local OIDC harness.";}
}
public sealed class WebOidcProtocolTests
{
    [LocalOidcFact]
    public async Task Real_provider_code_pkce_callback_cookie_logout_and_bad_callback()
    {
        var authority=Environment.GetEnvironmentVariable("BPC_E205_OIDC_AUTHORITY")??throw new InvalidOperationException("Run guarded local OIDC harness.");
        var password=Environment.GetEnvironmentVariable("BPC_E205_OIDC_PASSWORD")??throw new InvalidOperationException();
        var uri=new Uri(authority);Assert.True(uri.IsLoopback && uri.Scheme=="https");
        await using var host=new WebUiTests.Host(false,authority);using var browser=host.Browser();
        var challenge=await browser.GetAsync("/account/login");Assert.True(challenge.StatusCode==HttpStatusCode.Redirect);
        var authorize=challenge.Headers.Location!;
        // Compound assertion deliberately avoids printing authorization URLs carrying state/nonce.
        var pushed=QueryHelpers.ParseQuery(authorize.Query).ContainsKey("request_uri");
        Assert.True(pushed || authorize.AbsoluteUri.Contains("code_challenge_method=S256") && authorize.AbsoluteUri.Contains("nonce=") && authorize.AbsoluteUri.Contains("state="));
        Assert.True(host.Services.GetRequiredService<IOptionsMonitor<OpenIdConnectOptions>>().Get("WebOidc").UsePkce);
        using var handler=Business.Platform.Core.Api.Authentication.PrivateOidcTrust.Handler(Environment.GetEnvironmentVariable("BPC_E205_OIDC_CA")!);
        handler.AllowAutoRedirect=false;handler.CookieContainer=new CookieContainer();using var provider=new HttpClient(handler);
        var form=await provider.GetAsync(authorize);
        for(var redirect=0;redirect<6 && form.StatusCode==HttpStatusCode.Redirect;redirect++) {
            var target=form.Headers.Location!;Assert.True(target.Host==uri.Host && target.Port==uri.Port);
            form=await provider.GetAsync(target);
        }
        if(form.StatusCode!=HttpStatusCode.OK) {
            var rejected=await form.Content.ReadAsStringAsync();
            var signals=new[]{"Invalid parameter: request_uri","Invalid parameter: redirect_uri","Invalid parameter: client_id","Client not found","Client is not enabled","Client is not allowed","HTTPS required","Page has expired","Invalid request","Invalid parameter","invalid_request","Unexpected error"}.Where(rejected.Contains);
            throw new InvalidOperationException("Authorization page status "+(int)form.StatusCode+"; known signals: "+string.Join(',',signals));
        }
        var html=await form.Content.ReadAsStringAsync();var action=WebUtility.HtmlDecode(Regex.Match(html,"action=\"([^\"]*)\"").Groups[1].Value);Assert.True(Uri.TryCreate(action,UriKind.Absolute,out _));
        var signed=await provider.PostAsync(action,new FormUrlEncodedContent(new Dictionary<string,string>{{"username",Environment.GetEnvironmentVariable("BPC_E205_OIDC_USER")!},{"password",password},{"credentialId",""}}));
        Assert.True(signed.StatusCode==HttpStatusCode.Redirect);
        var callback=signed.Headers.Location!;Assert.True(callback.Host=="localhost" && callback.AbsolutePath=="/account/oidc-callback");
        var authenticated=await browser.GetAsync(callback.PathAndQuery);Assert.True(authenticated.StatusCode==HttpStatusCode.Redirect && authenticated.Headers.Location?.OriginalString=="/app");
        var app=await browser.GetAsync("/app");Assert.True(app.StatusCode==HttpStatusCode.OK);var page=await app.Content.ReadAsStringAsync();
        Assert.Contains("Visible only in A",page);
        Assert.True((await browser.GetAsync("/api/v1/me/context")).StatusCode==HttpStatusCode.Unauthorized);
        var bad=await browser.GetAsync("/account/oidc-callback?code=invalid&state=invalid");Assert.True(bad.StatusCode==HttpStatusCode.Redirect && bad.Headers.Location?.OriginalString=="/account/unavailable");
        var logout=await browser.PostAsync("/account/logout",new FormUrlEncodedContent(new Dictionary<string,string>{{"__RequestVerificationToken",WebUiTests.Hidden(page,"__RequestVerificationToken")}}));
        Assert.True(logout.StatusCode==HttpStatusCode.Redirect && logout.Headers.Location?.Host==uri.Host);
        Assert.True((await browser.GetAsync("/app")).StatusCode==HttpStatusCode.Redirect);
        var remoteLogout=await provider.GetAsync(logout.Headers.Location!);
        Assert.True(remoteLogout.StatusCode==HttpStatusCode.Redirect);
        Assert.True((await browser.GetAsync("/account/session")).StatusCode==HttpStatusCode.Unauthorized);
    }
}
