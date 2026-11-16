using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
namespace Business.Platform.Core.Api.Authentication;

/// <summary>Separate bearer audience/scheme. No enterprise identity registration on preview authentication.</summary>
public static class PlatformAuthentication
{
    public static void Add(IServiceCollection services,IConfiguration configuration)
    {
        var section=configuration.GetSection("PlatformAuthentication");
        services.Configure<PlatformSecurityOptions>(section);
        services.AddAuthentication().AddJwtBearer("Platform",options=>{
            var profile=section.Get<PlatformSecurityOptions>()??new();
            options.MapInboundClaims=false;options.IncludeErrorDetails=false;options.RequireHttpsMetadata=true;
            options.TokenValidationParameters=new TokenValidationParameters {
                AuthenticationType="Platform",ValidateIssuer=true,ValidateAudience=true,ValidateLifetime=true,
                ValidateIssuerSigningKey=true,RequireSignedTokens=true,RequireExpirationTime=true,
                ValidTypes=["at+jwt","application/at+jwt"],
                ValidAlgorithms=[SecurityAlgorithms.RsaSha256,SecurityAlgorithms.RsaSsaPssSha256,SecurityAlgorithms.EcdsaSha256],
                ClockSkew=TimeSpan.FromSeconds(30)
            };
            if(profile.IsConfigured && string.Equals(profile.Audience,configuration["Authentication:Audience"],StringComparison.Ordinal))
                throw new InvalidOperationException("Platform and enterprise audiences must differ.");
            if(profile.IsConfigured) {
                options.Authority=profile.Authority;options.Audience=profile.Audience;
                options.TokenValidationParameters.ValidIssuer=profile.Authority;
                if(!string.IsNullOrWhiteSpace(profile.CertificateAuthorityBase64)) options.BackchannelHttpHandler=PrivateOidcTrust.Handler(profile.CertificateAuthorityBase64);
            }
            options.BackchannelTimeout=TimeSpan.FromSeconds(4);
            options.Events=new JwtBearerEvents {
                OnTokenValidated=context=>{
                    var required=new[]{"iss","sub","iat","jti","client_id"};
                    if(required.Any(name=>context.Principal!.FindAll(name).Count()!=1||string.IsNullOrWhiteSpace(context.Principal.FindFirst(name)!.Value))||
                        !long.TryParse(context.Principal!.FindFirst("iat")!.Value,out var issued)||issued>DateTimeOffset.UtcNow.AddSeconds(30).ToUnixTimeSeconds()||
                        !string.Equals(context.SecurityToken.Issuer,context.Options.Authority,StringComparison.Ordinal))context.Fail("Invalid Platform access token.");
                    return Task.CompletedTask;
                },
                OnChallenge=async context=>{
                    context.HandleResponse();context.Response.Headers.WWWAuthenticate="Bearer";
                    await Results.Problem(statusCode:401,title:"Platform authentication required.",extensions:new Dictionary<string,object?>{["code"]="Authentication.Required"}).ExecuteAsync(context.HttpContext);
                },
                OnForbidden=context=>Results.Problem(statusCode:403,title:"Platform access unavailable.",extensions:new Dictionary<string,object?>{["code"]="Access.Unavailable"}).ExecuteAsync(context.HttpContext)
            };
        });
        services.AddAuthorizationBuilder().AddPolicy("PlatformOperator",policy=>policy.AddAuthenticationSchemes("Platform").RequireAuthenticatedUser().AddRequirements(new OperatorRequirement()));
        services.AddSingleton<IAuthorizationHandler,OperatorHandler>();
    }
    private sealed class OperatorRequirement:IAuthorizationRequirement;
    private sealed class OperatorHandler(IOptionsMonitor<PlatformSecurityOptions> options):AuthorizationHandler<OperatorRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,OperatorRequirement requirement)
        {
            if(PlatformEvidencePolicy.Allows(context.User,options.CurrentValue,DateTimeOffset.UtcNow))context.Succeed(requirement);
            return Task.CompletedTask;
        }
    }
}
