using System.Diagnostics.Metrics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Encodings.Web;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace Business.Platform.Core.Api.Web;

public sealed record ValidatedLogout(string Receipt,byte[] ContentHash,string? Subject,string? Sid,string Selector,long Issued,long Expires);

public sealed class LogoutTokenValidator(IOptionsMonitor<OpenIdConnectOptions> options,TimeProvider clock)
{
    public async Task<ValidatedLogout?> ValidateAsync(string raw,CancellationToken cancellation)
    {
        if(raw.Length>8192 || raw.Count(c=>c=='.')!=2)return null;
        try {
            var parts=raw.Split('.');using var header=JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0]));
            using var payload=JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[1]));
            var h=header.RootElement;var p=payload.RootElement;
            if(!Unique(h) || !Unique(p) || Text(h,"alg")!="RS256" || Text(h,"typ") is not ("JWT" or "logout+jwt") ||
               h.TryGetProperty("jku",out _) || h.TryGetProperty("jwk",out _) || h.TryGetProperty("x5u",out _) || h.TryGetProperty("crit",out _) || p.TryGetProperty("nonce",out _))return null;
            var jti=Text(p,"jti");var sub=Text(p,"sub");var sid=Text(p,"sid");
            if(jti is null || jti.Length>512 || (sub is null && sid is null) || sub?.Length>512 || sid?.Length>512 ||
               (p.TryGetProperty("sub",out _) && sub is null) || (p.TryGetProperty("sid",out _) && sid is null) ||
               !p.TryGetProperty("iat",out var i) || !i.TryGetInt64(out var issued) ||
               !p.TryGetProperty("exp",out var e) || !e.TryGetInt64(out var expires))return null;
            var now=clock.GetUtcNow().ToUnixTimeSeconds();
            if(issued<now-120 || issued>now+5 || expires<=now-5 || expires<=issued)return null;
            if(!p.TryGetProperty("events",out var events) || !Unique(events) || events.EnumerateObject().Count()!=1 ||
               !events.TryGetProperty("http://schemas.openid.net/event/backchannel-logout",out var action) ||
               action.ValueKind!=JsonValueKind.Object || action.EnumerateObject().Any())return null;
            var oidc=options.Get("WebOidc");
            Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration configuration;
            try {configuration=await oidc.ConfigurationManager!.GetConfigurationAsync(cancellation);}
            catch(InvalidOperationException) {throw new HttpRequestException("OIDC metadata unavailable.");}
            var parameters=new TokenValidationParameters {
                ValidateIssuer=true,ValidIssuer=oidc.Authority,ValidateAudience=true,ValidAudience=oidc.ClientId,
                IgnoreTrailingSlashWhenValidatingAudience=false,
                ValidateIssuerSigningKey=true,RequireSignedTokens=true,RequireExpirationTime=true,ValidateLifetime=true,
                ClockSkew=TimeSpan.FromSeconds(5),ValidAlgorithms=[SecurityAlgorithms.RsaSha256],ValidTypes=["JWT","logout+jwt"],
                IssuerSigningKeys=configuration.SigningKeys,
                ConfigurationManager=oidc.ConfigurationManager as BaseConfigurationManager
            };
            var handler=new JsonWebTokenHandler{MaximumTokenSizeInBytes=8192};
            var result=await handler.ValidateTokenAsync(raw,parameters);
            if(!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException) {
                oidc.ConfigurationManager.RequestRefresh();
                try {configuration=await oidc.ConfigurationManager.GetConfigurationAsync(cancellation);}
                catch(InvalidOperationException) {throw new HttpRequestException("OIDC metadata unavailable.");}
                parameters.IssuerSigningKeys=configuration.SigningKeys;
                result=await handler.ValidateTokenAsync(raw,parameters);
            }
            if(!result.IsValid)return null;
            var issuer=oidc.Authority!;var client=oidc.ClientId!;
            var subjectScope=sub is null?null:LogoutCorrelation.Hash("sub",issuer,client,sub);
            var sidScope=sid is null?null:LogoutCorrelation.Hash("sid",issuer,client,sid);
            var selector=sub is not null && sid is not null?LogoutCorrelation.Hash("pair",issuer,client,sub,sid):sidScope??subjectScope!;
            return new(LogoutCorrelation.Hash("jti",issuer,client,jti),SHA256.HashData(Encoding.UTF8.GetBytes(raw)),subjectScope,sidScope,selector,issued,expires);
        } catch(Exception error) when(error is JsonException or FormatException or ArgumentException or InvalidOperationException) {return null;}
    }
    private static bool Unique(JsonElement element)=>element.ValueKind==JsonValueKind.Object && element.EnumerateObject().Select(p=>p.Name).Distinct(StringComparer.Ordinal).Count()==element.EnumerateObject().Count();
    private static string? Text(JsonElement element,string name)=>element.TryGetProperty(name,out var value) && value.ValueKind==JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())?value.GetString():null;
}

// Authenticates nobody: the signed protocol message is validated in the endpoint.
public sealed class BackChannelTransport(IOptionsMonitor<AuthenticationSchemeOptions> options,ILoggerFactory logger,UrlEncoder encoder)
    :AuthenticationHandler<AuthenticationSchemeOptions>(options,logger,encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()=>Task.FromResult(AuthenticateResult.NoResult());
}

public static class BackChannelLogout
{
    public const string Path="/account/backchannel-logout";
    public static bool IsPath(PathString path)=>string.Equals(path.Value?.TrimEnd('/'),Path,StringComparison.OrdinalIgnoreCase);
    private static readonly Meter Meter=new("bpc.web.backchannel");
    private static readonly Counter<long> Outcomes=Meter.CreateCounter<long>("logout_requests");
    public static bool Enabled(IConfiguration config)=>config.GetValue<bool>("WebAuthentication:BackChannelLogout:Enabled");
    public static void Map(WebApplication app)
    {
        if(!Enabled(app.Configuration))return;
        app.MapPost(Path,Handle).AllowAnonymous().RequireRateLimiting("BackChannelLogout");
    }
    private static IResult Result(int status,string outcome)
    {
        Outcomes.Add(1,new KeyValuePair<string,object?>("result",outcome));
        return status==200?Results.Ok():Results.Json(new{error=status==503?"temporarily_unavailable":"invalid_request"},statusCode:status);
    }
    private static async Task<IResult> Handle(HttpContext http,LogoutTokenValidator validator,PostgresSessionStore database)
    {
        if(!http.Request.IsHttps || http.Request.QueryString.HasValue)return Result(400,"transport_invalid");
        if(http.Request.ContentType?.Split(';')[0].Trim()!="application/x-www-form-urlencoded")return Result(415,"format_invalid");
        if(http.Request.ContentLength>16384)return Result(413,"size_invalid");
        var buffer=new byte[16385];var length=0;
        while(length<buffer.Length) {var read=await http.Request.Body.ReadAsync(buffer.AsMemory(length),http.RequestAborted);if(read==0)break;length+=read;}
        if(length>16384)return Result(413,"size_invalid");
        string form;
        try{form=new UTF8Encoding(false,true).GetString(buffer,0,length);}catch(DecoderFallbackException){return Result(400,"form_invalid");}
        var values=QueryHelpers.ParseQuery(form);
        if(values.Count!=1 || !values.TryGetValue("logout_token",out var token) || token.Count!=1)return Result(400,"form_invalid");
        try {
            var logout=await validator.ValidateAsync(token[0]??"",http.RequestAborted);
            if(logout is null)return Result(400,"token_invalid");
            var first=await database.LogoutAsync(logout.Receipt,logout.ContentHash,logout.Subject,logout.Sid,logout.Selector,logout.Issued,logout.Expires);
            return Result(200,first?"committed":"duplicate");
        } catch(PostgresException error) when(error.SqlState=="P1702") {return Result(400,"replay_conflict");}
        catch(Exception error) when(error is NpgsqlException or TimeoutException or HttpRequestException or IOException) {
            return Result(503,"dependency_unavailable");
        }
    }
}
