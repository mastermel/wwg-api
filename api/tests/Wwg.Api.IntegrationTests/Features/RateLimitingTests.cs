using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
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
    public async Task Refresh_HasItsOwnLimit_SeparateFromSignIn()
    {
        using var client = App.WithWebHostBuilder(builder =>
                builder
                    .UseSetting("RateLimits:Auth:PermitLimit", "1")
                    .UseSetting("RateLimits:Refresh:PermitLimit", "2")
            )
            .CreateClient();
        using var login = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest("nobody@example.com", TestPassword),
            CancellationToken
        );

        // The sign-in used up the auth limit; refreshes still get theirs.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 3; i++)
        {
            using var refresh = await client.PostAsync(
                new Uri("/api/auth/refresh", UriKind.Relative),
                null,
                CancellationToken
            );
            statuses.Add(refresh.StatusCode);
        }

        Assert.Equal(
            [
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests,
            ],
            statuses
        );
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

    [Theory]
    [InlineData("POST", "/api/auth/register")]
    [InlineData("POST", "/api/auth/reset-password")]
    [InlineData("PUT", "/api/me/email")]
    [InlineData("PUT", "/api/me/password")]
    public async Task AuthLimitedEndpoint_OverTheLimit_Returns429(string method, string path)
    {
        using var app = App.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimits:Auth:PermitLimit", "2")
        );
        using var client = app.CreateClient();
        // Signing up takes the first of the two (the endpoints share the auth limit).
        using var registered = await RegisterAsync(client, "mel@example.com");
        var token = await registered.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token?.AccessToken
        );

        using var second = await SendEmptyAsync(client, method, path);
        using var third = await SendEmptyAsync(client, method, path);

        Assert.NotEqual(HttpStatusCode.TooManyRequests, second.StatusCode);
        await third.AssertProblemAsync(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task BehindATrustedProxy_EachForwardedClientHasItsOwnLimit()
    {
        using var client = ClientBehind(proxy: "10.0.0.1");

        var statuses = new[]
        {
            await LoginStatusAsync(client, forwardedFor: "203.0.113.1"),
            await LoginStatusAsync(client, forwardedFor: "203.0.113.2"),
            await LoginStatusAsync(client, forwardedFor: "203.0.113.1"),
        };

        Assert.Equal(
            [
                HttpStatusCode.Unauthorized,
                HttpStatusCode.Unauthorized,
                HttpStatusCode.TooManyRequests,
            ],
            statuses
        );
    }

    [Fact]
    public async Task FromAnUntrustedAddress_ForwardedHeadersAreIgnored()
    {
        // Not a known proxy: X-Forwarded-For is the client's own claim, so it can't dodge the limit.
        using var client = ClientBehind(proxy: "198.51.100.7");

        var statuses = new[]
        {
            await LoginStatusAsync(client, forwardedFor: "203.0.113.1"),
            await LoginStatusAsync(client, forwardedFor: "203.0.113.2"),
        };

        Assert.Equal([HttpStatusCode.Unauthorized, HttpStatusCode.TooManyRequests], statuses);
    }

    private static async Task<HttpResponseMessage> SendEmptyAsync(
        HttpClient client,
        string method,
        string path
    )
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json"),
        };
        return await client.SendAsync(request, CancellationToken);
    }

    /// <summary>
    /// A client whose requests arrive from <paramref name="proxy"/> (in-process requests have no
    /// address of their own), with 10.0.0.1 as the trusted proxy and a sign-in limit of 1.
    /// </summary>
    private HttpClient ClientBehind(string proxy) =>
        App.WithWebHostBuilder(builder =>
                builder
                    .UseSetting("RateLimits:Auth:PermitLimit", "1")
                    .UseSetting("ForwardedHeaders:KnownProxies:0", "10.0.0.1")
                    .ConfigureTestServices(services =>
                        services.AddSingleton<IStartupFilter>(new RemoteIpAddress(proxy))
                    )
            )
            .CreateClient();

    private static async Task<HttpStatusCode> LoginStatusAsync(
        HttpClient client,
        string forwardedFor
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest("nobody@example.com", TestPassword)),
        };
        request.Headers.Add("X-Forwarded-For", forwardedFor);
        using var response = await client.SendAsync(request, CancellationToken);
        return response.StatusCode;
    }

    /// <summary>Sets the connection's remote address, before the app's own middleware runs.</summary>
    private sealed class RemoteIpAddress(string address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(
                    (context, nextMiddleware) =>
                    {
                        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
                        return nextMiddleware(context);
                    }
                );
                next(app);
            };
    }
}
