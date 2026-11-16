using Business.Platform.Core.Api;

if (args.SequenceEqual(new[] { "--healthcheck" }))
{
    try
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/live");
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException) { return 1; }
    catch (TaskCanceledException) { return 1; }
}

var app = ApiComposition.Create(args);
await app.RunAsync();
return 0;

public partial class Program;
