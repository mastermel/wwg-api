using System.Net;
using System.Text.Json;

namespace Wwg.Api.IntegrationTests;

public sealed class HealthTests(WwgApiFactory factory) : IClassFixture<WwgApiFactory>
{
    [Fact]
    public async Task GetHealth_AllChecksPass_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("Healthy", body.RootElement.GetProperty("status").GetString());
    }
}
