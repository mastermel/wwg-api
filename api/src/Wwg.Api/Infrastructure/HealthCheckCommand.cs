namespace Wwg.Api.Infrastructure;

/// <summary>
/// <c>dotnet Wwg.Api.dll --health-check</c>: asks the running app's <c>GET /health</c> and exits
/// 0 (healthy) or 1, for the container's HEALTHCHECK. The runtime image has no curl or wget.
/// </summary>
internal static class HealthCheckCommand
{
    public const string Argument = "--health-check";

    public static async Task<int> RunAsync(Uri? baseAddress = null)
    {
        // The port the container listens on (the .NET images set ASPNETCORE_HTTP_PORTS=8080).
        var port =
            Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split([',', ';'])[0]
            ?? "8080";
        baseAddress ??= new Uri($"http://localhost:{port}");

        using var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(5),
        };
        try
        {
            using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
