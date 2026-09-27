using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wwg.Api.IntegrationTests;

public sealed class SmokeTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task App_UnknownRoute_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/does-not-exist", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
