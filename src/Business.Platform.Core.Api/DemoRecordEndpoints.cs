using System.Text.Json.Serialization;
using System.Text.Json;
using Business.Platform.Core.Application.Tenancy;
using Business.Platform.Core.Infrastructure.Persistence;

namespace Business.Platform.Core.Api;

public static class DemoRecordEndpoints
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record Input(string? Title);
    public static void AddServices(IServiceCollection services)
    {
        services.AddScoped<ManageDemoRecords>();
        services.AddSingleton<IDemoRecordStore,PostgresDemoRecordStore>();
    }
    private static object Dto(DemoRecord item)=>new{item.Id,item.OrganizationId,item.Title,resourceVersion=DemoRecordVersion.Encode(item)};
    private static IResult Problem(string code)=>Results.Problem(statusCode:code switch{
        "Authentication.Required"=>401,"Access.Forbidden"=>403,"Resource.NotFound"=>404,
        "DemoRecord.VersionRequired"=>428,"DemoRecord.VersionConflict"=>412,
        "DemoRecord.InvalidVersion" or "DemoRecord.InvalidInput" or "DemoRecord.InvalidPage"=>400,
        "Idempotency.PayloadMismatch" or "Idempotency.Expired"=>409,"Context.Inconsistent"=>500,_=>503},
        title:"DemoRecord operation cannot be completed.",extensions:new Dictionary<string,object?>{{"code",code}});
    private static bool Unavailable(HttpContext http)=>http.Items.ContainsKey("IdentityBootstrapUnavailable");
    private static async Task<Input?> ReadInput(HttpContext http,CancellationToken token)
    {
        try { return http.Request.HasJsonContentType()?await http.Request.ReadFromJsonAsync<Input>(token):null; }
        catch(JsonException) { return null; }
        catch(BadHttpRequestException) { return null; }
    }
    public static void Map(WebApplication app)
    {
        const string path="/api/v1/organizations/{orgId:guid}/demo-records";
        app.MapGet(path,async(Guid orgId,ManageDemoRecords useCase,HttpContext http,CancellationToken token)=>{
            if(Unavailable(http)) return Problem("Dependency.Unavailable");
            var query=http.Request.Query;
            var limit=!query.ContainsKey("limit")?25:int.TryParse(query["limit"],out var size)?size:0;
            var offset=!query.ContainsKey("offset")?0:int.TryParse(query["offset"],out var skip)?skip:-1;
            var result=await useCase.ListAsync(orgId,limit,offset,token);
            return result.IsFailure?Problem(result.Error.Code):Results.Ok(new{items=result.Value.Items.Select(Dto),result.Value.HasMore});
        }).RequireAuthorization();
        app.MapGet(path+"/{id:guid}",async(Guid orgId,Guid id,ManageDemoRecords useCase,HttpContext http,CancellationToken token)=>{
            if(Unavailable(http)) return Problem("Dependency.Unavailable");
            var result=await useCase.ReadAsync(orgId,id,token);
            if(result.IsFailure) return Problem(result.Error.Code);
            http.Response.Headers.ETag=DemoRecordVersion.Encode(result.Value);return Results.Ok(Dto(result.Value));
        }).RequireAuthorization();
        app.MapPost(path,async(Guid orgId,ManageDemoRecords useCase,HttpContext http,CancellationToken token)=>{
            if(Unavailable(http)) return Problem("Dependency.Unavailable");
            var input=await ReadInput(http,token);
            if(input is null) return Problem("DemoRecord.InvalidInput");
            Guid.TryParseExact(http.Request.Headers["Idempotency-Key"].ToString(),"D",out var key);
            var result=await useCase.CreateAsync(orgId,input.Title,key,token);
            if(result.IsFailure) return Problem(result.Error.Code);
            var value=result.Value;http.Response.Headers.ETag=DemoRecordVersion.Encode(value.Item);
            return value.Replayed?Results.Ok(Dto(value.Item)):Results.Created($"/api/v1/organizations/{orgId:D}/demo-records/{value.Item.Id:D}",Dto(value.Item));
        }).RequireAuthorization();
        app.MapPatch(path+"/{id:guid}",async(Guid orgId,Guid id,ManageDemoRecords useCase,HttpContext http,CancellationToken token)=>{
            if(Unavailable(http)) return Problem("Dependency.Unavailable");
            var input=await ReadInput(http,token);
            if(input is null) return Problem("DemoRecord.InvalidInput");
            var result=await useCase.UpdateAsync(orgId,id,input.Title,http.Request.Headers.IfMatch.Count==0?null:http.Request.Headers.IfMatch.ToString(),token);
            if(result.IsFailure) return Problem(result.Error.Code);
            http.Response.Headers.ETag=DemoRecordVersion.Encode(result.Value);return Results.Ok(Dto(result.Value));
        }).RequireAuthorization();
    }
}
