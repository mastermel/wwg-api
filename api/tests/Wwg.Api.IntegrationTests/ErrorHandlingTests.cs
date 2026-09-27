using System.Net;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class ErrorHandlingTests : ApiTest
{
    [Fact]
    public async Task Get_UnknownApiRoute_ReturnsProblemDetails404()
    {
        using var response = await Client.GetAsync(
            new Uri("/api/does-not-exist", UriKind.Relative),
            CancellationToken
        );

        var problem = await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal("GET /api/does-not-exist", problem.Instance);
    }
}
