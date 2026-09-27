using System.Net;
using System.Text.Json;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class HealthTests : ApiTest
{
    [Fact]
    public async Task GetHealth_AllChecksPass_ReturnsHealthy()
    {
        using var response = await Client.GetAsync(
            new Uri("/health", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await StatusAsync(response));
    }

    [Fact]
    public async Task GetHealth_DatabaseHasNoSchema_ReturnsUnhealthy503()
    {
        // A fresh, empty in-memory database: nothing keeps it alive, so it stays empty.
        using var client = App.WithWebHostBuilder(builder =>
                builder.UseSetting(
                    "ConnectionStrings:Default",
                    $"Data Source=empty-{Guid.CreateVersion7():N};Mode=Memory;Cache=Shared"
                )
            )
            .CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await StatusAsync(response));
    }

    private static async Task<string?> StatusAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(CancellationToken)
        );
        return body.RootElement.GetProperty("status").GetString();
    }
}
