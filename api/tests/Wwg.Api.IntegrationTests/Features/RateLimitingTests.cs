using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class RateLimitingTests : ApiTest
{
    [Fact]
    public async Task Login_OverTheAuthLimit_Returns429WithRetryAfter()
    {
        using var client = App.WithWebHostBuilder(builder =>
                builder.UseSetting("RateLimits:Auth:PermitLimit", "3")
            )
            .CreateClient();
        var login = new LoginRequest("nobody@example.com", TestPassword);

        for (var i = 0; i < 3; i++)
        {
            using var allowed = await client.PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                login,
                CancellationToken
            );
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            login,
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.TooManyRequests);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);
    }

    [Fact]
    public async Task ForgotPassword_OverTheEmailLimit_Returns429()
    {
        using var client = App.WithWebHostBuilder(builder =>
                builder.UseSetting("RateLimits:Email:PermitLimit", "1")
            )
            .CreateClient();
        var request = new ForgotPasswordRequest("nobody@example.com");

        using var allowed = await client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            request,
            CancellationToken
        );
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            request,
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, allowed.StatusCode);
        await response.AssertProblemAsync(HttpStatusCode.TooManyRequests);
    }
}
