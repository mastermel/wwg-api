using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Wwg.Api.IntegrationTests;

public sealed class ErrorHandlingTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_UnknownApiRoute_ReturnsProblemDetails404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/api/does-not-exist", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(problem);
        Assert.Equal(404, problem.Status);
        Assert.Equal("GET /api/does-not-exist", problem.Instance);
    }
}
