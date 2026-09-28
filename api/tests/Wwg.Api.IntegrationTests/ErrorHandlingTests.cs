using System.Net;
using System.Text;
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

    [Fact]
    public async Task Post_MalformedJson_ReturnsProblemDetails400()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        using var response = await Client.PostAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            content,
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }
}
