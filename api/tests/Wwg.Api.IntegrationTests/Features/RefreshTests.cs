using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class RefreshTests : ApiTest
{
    private static Task<HttpResponseMessage> RefreshAsync(HttpClient client) =>
        client.PostAsync(new Uri("/api/auth/refresh", UriKind.Relative), null, CancellationToken);

    private static string? RefreshCookie(HttpResponseMessage response) =>
        response
            .Headers.GetValues("Set-Cookie")
            .SingleOrDefault(c => c.StartsWith("__Secure-wwg-refresh=", StringComparison.Ordinal));

    private async Task WithUserManagerAsync(Func<UserManager<AppUser>, AppUser, Task> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync("mel@example.com");
        await action(users, user!); // Registered by the test just before.
    }

    [Fact]
    public async Task Refresh_ValidCookie_ReturnsNewAccessTokenAndNewCookie()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await RefreshAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        Assert.False(string.IsNullOrEmpty(token?.AccessToken));
        Assert.NotNull(RefreshCookie(response));
    }

    [Fact]
    public async Task Refresh_NoCookie_Returns401()
    {
        using var response = await RefreshAsync(Client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_UnreadableCookie_Returns401AndClearsIt()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", "__Secure-wwg-refresh=not-a-real-token");

        using var response = await Client.SendAsync(request, CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
        Assert.Contains("expires=", RefreshCookie(response), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_After30Days_Returns401()
    {
        using var client = await CreateUserClientAsync();

        Clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromMinutes(1));
        using var response = await RefreshAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_UsedWithin30Days_KeepsTheSessionGoingPast30Days()
    {
        using var client = await CreateUserClientAsync();

        Clock.Advance(TimeSpan.FromDays(20));
        (await RefreshAsync(client)).EnsureSuccessStatusCode();
        Clock.Advance(TimeSpan.FromDays(20));
        using var response = await RefreshAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_SecurityStampChanged_Returns401()
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        await WithUserManagerAsync((users, user) => users.UpdateSecurityStampAsync(user));

        using var response = await RefreshAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_UserDeleted_Returns401()
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        await WithUserManagerAsync((users, user) => users.DeleteAsync(user));

        using var response = await RefreshAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_ClearsTheCookie_SoRefreshFails()
    {
        using var client = await CreateUserClientAsync();

        using var logout = await client.PostAsync(
            new Uri("/api/auth/logout", UriKind.Relative),
            null,
            CancellationToken
        );
        using var refresh = await RefreshAsync(client);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        await refresh.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }
}
