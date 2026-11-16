using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;

namespace Business.Platform.Core.Api;

public static class InvitationEndpoints
{
    public sealed record IssueRequest(Guid RecipientUserId,string RecipientReference)
    { public override string ToString()=>"IssueRequest [redacted]"; }
    public sealed record CodeRequest(string Code)
    { public override string ToString()=>"CodeRequest [redacted]"; }
    public static void AddServices(IServiceCollection services)
    {
        services.AddScoped<Invitations>();services.AddSingleton<IInvitationStore,PostgresInvitationStore>();
    }
    private static string? Match(HttpContext http)=>http.Request.Headers.IfMatch.Count==0?null:http.Request.Headers.IfMatch.ToString();
    private static string? Key(HttpContext http)=>http.Request.Headers["Idempotency-Key"].ToString();
    private static IResult Problem(string code)=>Results.Problem(statusCode:code switch {
        "Authentication.Required"=>401,"Access.Forbidden"=>403,"Invitation.Unavailable" or "Recipient.Unavailable"=>404,
        "Invitation.InvalidRequest"=>400,"Invitation.VersionRequired"=>428,"Invitation.VersionConflict"=>412,
        "Invitation.Conflict" or "Invitation.Expired" or "Invitation.Cancelled" or "Invitation.Replaced" or "Invitation.AlreadyAccepted" or "Idempotency.PayloadMismatch"=>409,
        "RateLimit.Exceeded"=>429,"Context.Inconsistent"=>500,_=>503
    },title:"Invitation operation cannot be completed.",extensions:new Dictionary<string,object?>{{"code",code}});
    private static object Metadata(InvitationDetails i)=>new {i.InvitationId,i.OrganizationId,i.OrganizationName,i.RecipientUserId,i.Role,effectiveStatus=i.State,i.ExpiresAtUtc,resourceVersion=InvitationVersion.Encode(i),i.AccountId,i.MembershipId};
    private static IResult Outcome(Business.Platform.Core.Domain.Shared.Result<InvitationOutcome> result,HttpContext http)
    {
        if(result.IsFailure)return Problem(result.Error.Code);
        http.Response.Headers.ETag=InvitationVersion.Encode(result.Value.Invitation);
        return Results.Ok(new {invitation=Metadata(result.Value.Invitation),replayed=result.Value.Replayed});
    }
    private static IResult Issued(Business.Platform.Core.Domain.Shared.Result<IssuedInvitation> result,HttpContext http,bool creation)
    {
        if(result.IsFailure)return Problem(result.Error.Code);
        var r=result.Value;http.Response.Headers.ETag=InvitationVersion.Encode(r.Invitation);
        return Results.Json(new {invitation=Metadata(r.Invitation),r.Code,r.Replayed,r.SecretUnavailable,url=$"/app/invitations/{r.Invitation.InvitationId:D}"},statusCode:creation&&!r.Replayed?201:200);
    }
    public static void Map(WebApplication app)
    {
        app.MapPost("/api/v1/organizations/{orgId:guid}/invitations",async(Guid orgId,IssueRequest body,Invitations useCase,HttpContext http,CancellationToken token)=>
            Issued(await useCase.IssueAsync(orgId,body.RecipientUserId,body.RecipientReference,Key(http),token),http,true)).WithMetadata(new PlatformEndpoints.PreviewLimit()).RequireAuthorization();
        app.MapGet("/api/v1/organizations/{orgId:guid}/invitations",async(Guid orgId,int? limit,string? cursor,string? state,Invitations useCase,HttpContext http,CancellationToken token)=>{
            var r=await useCase.ListAsync(orgId,limit??25,cursor,state,token);return r.IsFailure?Problem(r.Error.Code):Results.Ok(new {items=r.Value.Items.Select(Metadata),r.Value.NextCursor});
        }).RequireAuthorization();
        app.MapGet("/api/v1/organizations/{orgId:guid}/invitations/{id:guid}",async(Guid orgId,Guid id,Invitations useCase,HttpContext http,CancellationToken token)=>
            Outcome(await useCase.GetAsync(orgId,id,token),http)).RequireAuthorization();
        foreach(var replace in new[]{false,true}) {
            var command=replace;
            app.MapPost("/api/v1/organizations/{orgId:guid}/invitations/{id:guid}/"+(replace?"replace":"cancel"),async(Guid orgId,Guid id,Invitations useCase,HttpContext http,CancellationToken token)=>
                Issued(await useCase.ChangeAsync(orgId,id,command,Match(http),Key(http),token),http,command)).WithMetadata(new PlatformEndpoints.PreviewLimit()).RequireAuthorization();
        }
        foreach(var accept in new[]{false,true}) {
            var command=accept;
            app.MapPost("/api/v1/me/invitations/{id:guid}/"+(accept?"accept":"preview"),async(Guid id,CodeRequest body,Invitations useCase,HttpContext http,CancellationToken token)=>
                Outcome(await useCase.RecipientAsync(id,body.Code,command,Match(http),token),http)).WithMetadata(new PlatformEndpoints.PreviewLimit()).RequireAuthorization();
        }
    }
}
