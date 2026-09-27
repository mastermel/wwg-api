using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wwg.Api.IntegrationTests;

public sealed class OpenApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetDocument_Always_DescribesTheApiWithoutServers()
    {
        using var client = factory.CreateClient();

        using var document = JsonDocument.Parse(
            await client.GetStringAsync(
                new Uri("/openapi/v1.json", UriKind.Relative),
                TestContext.Current.CancellationToken
            )
        );

        var root = document.RootElement;
        Assert.Equal("wwg API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.False(root.TryGetProperty("servers", out _));
        var health = root.GetProperty("paths").GetProperty("/health").GetProperty("get");
        Assert.Equal("GetHealth", health.GetProperty("operationId").GetString());
        Assert.False(string.IsNullOrEmpty(health.GetProperty("summary").GetString()));
    }

    [Fact]
    public async Task GetSwaggerUi_Development_IsServed()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/swagger/index.html", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.NotFound)]
    [InlineData(true, HttpStatusCode.OK)]
    public async Task GetSwaggerUi_Production_IsServedOnlyWhenEnabled(
        bool enabled,
        HttpStatusCode expected
    )
    {
        using var production = factory.WithWebHostBuilder(builder =>
            builder
                .UseEnvironment("Production")
                .UseSetting("App:PublicUrl", "https://wwg.example.com")
                .UseSetting("App:EnableSwaggerUi", enabled.ToString())
        );
        using var client = production.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/swagger/index.html", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(expected, response.StatusCode);
    }
}
