using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

/// <summary>The API serving the built front-end, as in the Docker image (wwwroot = web/dist).</summary>
public sealed class SpaHostingTests : ApiTest
{
    private readonly string _webRoot = Directory.CreateTempSubdirectory("wwg-webroot-").FullName;

    public SpaHostingTests()
    {
        File.WriteAllText(
            Path.Combine(_webRoot, "index.html"),
            "<!doctype html><title>app</title>"
        );
        File.WriteAllText(Path.Combine(_webRoot, "sw.js"), "// service worker");
        File.WriteAllText(Path.Combine(_webRoot, "manifest.webmanifest"), "{}");
        Directory.CreateDirectory(Path.Combine(_webRoot, "assets"));
        File.WriteAllText(Path.Combine(_webRoot, "assets", "index-abc123.js"), "// app");
    }

    protected override ValueTask DisposeTestAsync()
    {
        Directory.Delete(_webRoot, recursive: true);
        return ValueTask.CompletedTask;
    }

    private WebApplicationFactory<Program> WithSpa() =>
        App.WithWebHostBuilder(builder => builder.UseWebRoot(_webRoot));

    private async Task<HttpResponseMessage> GetAsync(string path, bool withSpa = true)
    {
        var client = withSpa ? WithSpa().CreateClient() : Client;
        return await client.GetAsync(new Uri(path, UriKind.Relative), CancellationToken);
    }

    [Fact]
    public async Task Get_ClientSideRoute_ReturnsIndexHtmlWithNoCache()
    {
        using var response = await GetAsync("/campaigns/123");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains(
            "<title>app</title>",
            await response.Content.ReadAsStringAsync(CancellationToken),
            StringComparison.Ordinal
        );
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Get_HashedAsset_IsCachedImmutably()
    {
        using var response = await GetAsync("/assets/index-abc123.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "public, max-age=31536000, immutable",
            response.Headers.CacheControl?.ToString()
        );
    }

    [Theory]
    [InlineData("/sw.js")]
    [InlineData("/manifest.webmanifest")]
    public async Task Get_ServiceWorkerOrManifest_IsNeverCachedStale(string path)
    {
        using var response = await GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Get_Manifest_HasTheManifestContentType()
    {
        using var response = await GetAsync("/manifest.webmanifest");

        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Get_FrontEndPage_HasSecurityHeaders()
    {
        using var response = await GetAsync("/");

        var csp = Assert.Single(response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("script-src 'self'", csp, StringComparison.Ordinal);
        // The campaign map's tile hosts, and MapLibre's workers.
        Assert.Contains(
            "connect-src 'self' https://tiles.openfreemap.org https://tiles.mapterhorn.com;",
            csp,
            StringComparison.Ordinal
        );
        Assert.Contains("worker-src 'self' blob:;", csp, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", csp, StringComparison.Ordinal);
        Assert.Equal(
            "nosniff",
            Assert.Single(response.Headers.GetValues("X-Content-Type-Options"))
        );
        Assert.Single(response.Headers.GetValues("Referrer-Policy"));
    }

    [Theory]
    [InlineData("/api/does-not-exist")]
    [InlineData("/api/campaigns/123/nothing")]
    [InlineData("/health/extra")]
    [InlineData("/openapi/nothing")]
    public async Task Get_UnknownServerPath_ReturnsProblemDetails404NotTheApp(string path)
    {
        using var response = await GetAsync(path);

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_ClientSideRoute_WithoutABuiltFrontEnd_Returns404()
    {
        using var response = await GetAsync("/campaigns/123", withSpa: false);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_MissingFile_ReturnsProblemDetails404NotTheApp()
    {
        using var response = await GetAsync("/assets/missing.js");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Get_Health_StillReachesTheApi()
    {
        using var response = await GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }
}
