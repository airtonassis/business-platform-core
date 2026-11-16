using Business.Platform.Core.Api.Authentication;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Domain.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Business.Platform.Core.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.RateLimiting;

namespace Business.Platform.Core.Api.Web;

public static class WebComposition
{
    public const string Policy="TenantWeb";
    public static bool Enabled(IConfiguration config)=>config.GetValue<bool>("WebAuthentication:Enabled");
    public static void Add(IServiceCollection services,IConfiguration configuration,IHostEnvironment environment)
    {
        if(!Enabled(configuration)) {
            if(BackChannelLogout.Enabled(configuration))throw new InvalidOperationException("Back-channel requires web authentication.");
            return;
        }
        if(!environment.IsDevelopment())throw new InvalidOperationException("Local web session adapter requires Development; production store is not provisioned.");
        var config=configuration.GetSection("WebAuthentication");
        if(!Uri.TryCreate(config["Authority"],UriKind.Absolute,out var authority) || string.IsNullOrWhiteSpace(config["ClientId"]))throw new InvalidOperationException("Web OIDC configuration required.");
        if(authority.Scheme!="https")throw new InvalidOperationException("HTTPS OIDC authority required.");
        // Hosting request-start logs include callback query strings. Never log protocol credentials.
        services.AddLogging(logging=>{
            logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics",LogLevel.Warning);
            logging.AddFilter("Microsoft.AspNetCore.Authentication.OpenIdConnect",LogLevel.None);
        });
        services.AddSingleton<TimeProvider>(TimeProvider.System);
        var mode=config["SessionStore"]??"Memory";
        if(BackChannelLogout.Enabled(configuration)) {
            if(mode!="Postgres")throw new InvalidOperationException("Back-channel requires persistent sessions.");
            services.AddSingleton<LogoutTokenValidator>();
            services.AddAuthentication().AddScheme<AuthenticationSchemeOptions,BackChannelTransport>("BackChannel",_=>{});
            services.AddRateLimiter(options=>{options.RejectionStatusCode=429;options.AddFixedWindowLimiter("BackChannelLogout",limiter=>{
                limiter.PermitLimit=30;limiter.Window=TimeSpan.FromSeconds(10);limiter.QueueLimit=0;
            });});
        }
        if(mode=="Postgres")AddPersistentSessions(services,config,environment);
        else if(mode=="Memory") {
            services.AddSingleton<WebTicketStore>();
            services.AddOptions<CookieAuthenticationOptions>("Web").Configure<WebTicketStore>((options,store)=>options.SessionStore=store);
        }
        else throw new InvalidOperationException("Unknown web session store.");
        services.AddAuthentication().AddCookie("Web",options=>{
            options.Cookie.Name="__Host-bpc-web";options.Cookie.HttpOnly=true;options.Cookie.SecurePolicy=CookieSecurePolicy.Always;
            options.Cookie.SameSite=SameSiteMode.Lax;options.Cookie.Path="/";options.ExpireTimeSpan=TimeSpan.FromMinutes(30);
            options.SlidingExpiration=false;options.LoginPath="/account/login";options.AccessDeniedPath="/account/denied";
        }).AddOpenIdConnect("WebOidc",options=>{
            options.SignInScheme="Web";options.SignOutScheme="Web";options.Authority=authority.AbsoluteUri.TrimEnd('/');
            options.ClientId=config["ClientId"];options.ClientSecret=config["ClientSecret"];
            options.ResponseType=OpenIdConnectResponseType.Code;options.ResponseMode=OpenIdConnectResponseMode.Query;options.UsePkce=true;
            options.MapInboundClaims=false;options.RequireHttpsMetadata=true;options.SaveTokens=true;
            options.BackchannelTimeout=TimeSpan.FromSeconds(4);
            options.ClaimActions.Clear();
            options.Scope.Clear();options.Scope.Add("openid");options.Scope.Add("profile");
            options.CallbackPath="/account/oidc-callback";options.SignedOutCallbackPath="/account/oidc-signed-out";
            options.CorrelationCookie.SecurePolicy=CookieSecurePolicy.Always;options.NonceCookie.SecurePolicy=CookieSecurePolicy.Always;
            options.TokenValidationParameters.ValidateIssuer=true;options.TokenValidationParameters.ValidateAudience=true;
            options.TokenValidationParameters.ValidIssuer=authority.AbsoluteUri.TrimEnd('/');options.TokenValidationParameters.ClockSkew=TimeSpan.FromSeconds(30);
            if(!string.IsNullOrWhiteSpace(config["CertificateAuthorityBase64"]))options.BackchannelHttpHandler=PrivateOidcTrust.Handler(config["CertificateAuthorityBase64"]!);
            options.Events=new OpenIdConnectEvents {
                OnRedirectToIdentityProvider=async context=>{
                    await context.HttpContext.SignOutAsync("Web");
                    context.ProtocolMessage.Prompt="login";
                    context.Response.Headers["Clear-Site-Data"]="\"cache\", \"storage\"";
                },
                OnTokenValidated=async context=>{
                    var user=context.Principal!;
                    if(user.FindAll("sub").Count()!=1 || user.FindAll("iss").Count()!=1 || string.IsNullOrWhiteSpace(user.FindFirst("sub")?.Value) || user.HasClaim(c=>c.Type==WebTicketStore.SessionClaim)) {context.Fail("Invalid identity.");return;}
                    if(BackChannelLogout.Enabled(configuration) && (user.FindAll("iat").Count()!=1 || !long.TryParse(user.FindFirst("iat")?.Value,out var issued) || issued>DateTimeOffset.UtcNow.AddSeconds(5).ToUnixTimeSeconds() ||
                       user.FindAll("sid").Count()!=1 || (user.FindFirst("sid") is {} sid && (string.IsNullOrWhiteSpace(sid.Value) || sid.Value.Length>512)))) {context.Fail("Invalid session correlation.");return;}
                    await context.HttpContext.RequestServices.GetRequiredService<IAuthenticatedIdentityBootstrap>().RegisterAsync(
                        new OidcSubject(user.FindFirst("iss")!.Value,user.FindFirst("sub")!.Value),context.HttpContext.RequestAborted);
                },
                OnRemoteFailure=context=>{context.HandleResponse();context.Response.Redirect("/account/unavailable");return Task.CompletedTask;},
                OnAuthenticationFailed=context=>{context.HandleResponse();context.Response.Redirect("/account/unavailable");return Task.CompletedTask;}
            };
        });
        services.AddAuthorizationBuilder().AddPolicy(Policy,new AuthorizationPolicyBuilder("Web").RequireAuthenticatedUser().Build());
        services.AddRazorPages(options=>{
            options.Conventions.AuthorizeFolder("/Tenant",Policy);
        });
        services.AddAntiforgery(options=>{options.Cookie.Name="__Host-bpc-csrf";options.Cookie.SecurePolicy=CookieSecurePolicy.Always;options.Cookie.HttpOnly=true;});
    }
    private static void AddPersistentSessions(IServiceCollection services,IConfigurationSection config,IHostEnvironment environment)
    {
        var section=config.GetSection("DataProtection");
        var directory=section["KeyRingPath"];var path=section["CertificatePath"];var passwordPath=section["CertificatePasswordFile"];
        var application=section["ApplicationName"];
        if(string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory) ||
           string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
           string.IsNullOrWhiteSpace(passwordPath) || !Path.IsPathFullyQualified(passwordPath) || string.IsNullOrWhiteSpace(application))
            throw new InvalidOperationException("Persistent web protection configuration required.");
        X509Certificate2 certificate;
        try {
            certificate=X509CertificateLoader.LoadPkcs12FromFile(path,File.ReadAllText(passwordPath).Trim(),X509KeyStorageFlags.EphemeralKeySet);
            if(!certificate.HasPrivateKey || certificate.NotAfter.ToUniversalTime()<=DateTime.UtcNow || certificate.NotBefore.ToUniversalTime()>DateTime.UtcNow)
                throw new InvalidOperationException();
        } catch(Exception error) when(error is IOException or System.Security.Cryptography.CryptographicException or InvalidOperationException) {
            throw new InvalidOperationException("Persistent web protection material unavailable.");
        }
        var databaseOptions=new PostgresOptions();config.GetSection("SessionDatabase").Bind(databaseOptions);
        // Eager validation, no database connection or schema creation at composition time.
        var database=new PostgresSessionStore(databaseOptions);
        services.AddSingleton(_=>database);
        services.AddSingleton(_=>certificate);
        services.AddDataProtection().SetApplicationName($"bpc:web:{environment.EnvironmentName}:{application}")
            .PersistKeysToFileSystem(new DirectoryInfo(directory)).ProtectKeysWithCertificate(certificate);
        services.AddSingleton(sp=>new PostgresWebTicketStore(sp.GetRequiredService<PostgresSessionStore>(),sp.GetRequiredService<IDataProtectionProvider>(),config["ClientId"]!,config.GetValue<bool>("BackChannelLogout:Enabled")));
        services.AddOptions<CookieAuthenticationOptions>("Web").Configure<PostgresWebTicketStore>((options,store)=>options.SessionStore=store);
    }
    public static void Map(WebApplication app)
    {
        if(!Enabled(app.Configuration))return;
        app.MapRazorPages();
        BackChannelLogout.Map(app);
        app.MapGet("/account/login",()=>Results.Challenge(new AuthenticationProperties{RedirectUri="/app"},["WebOidc"])).AllowAnonymous();
        app.MapGet("/account/denied",()=>Results.Content("Access unavailable.","text/plain",statusCode:403)).AllowAnonymous();
        app.MapGet("/account/unavailable",()=>Results.Content("Sign-in unavailable. No business operation was repeated.","text/plain",statusCode:503)).AllowAnonymous();
        app.MapGet("/account/session",async(HttpContext http,ResolveEnterpriseContext resolve)=>{
            var session=await http.AuthenticateAsync("Web");
            if(!session.Succeeded)return Results.Unauthorized();
            var context=await resolve.ExecuteAsync(null,http.RequestAborted);
            if(context.IsFailure)return Results.StatusCode(503);
            if(context.Value.State is not (EnterpriseContextState.Allowed or EnterpriseContextState.NotLinked))return Results.StatusCode(403);
            return Results.Ok(new{expiresAtUtc=session.Properties!.ExpiresUtc,stamp=SessionStamp(session.Principal!)});
        }).AllowAnonymous();
    }
    public static string SessionStamp(System.Security.Claims.ClaimsPrincipal user)=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(user.FindFirst(WebTicketStore.SessionClaim)?.Value??"")));
}
