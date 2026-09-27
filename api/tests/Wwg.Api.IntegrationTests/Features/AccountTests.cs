using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class AccountTests : ApiTest
{
    private static Task<HttpResponseMessage> GetMeAsync(HttpClient client) =>
        client.GetAsync(new Uri("/api/me", UriKind.Relative), CancellationToken);

    private async Task WithUserAsync(string email, Func<UserManager<AppUser>, AppUser, Task> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user =
            await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"No user {email}");
        await action(users, user);
    }

    [Fact]
    public async Task GetMe_SignedIn_ReturnsTheProfile()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await GetMeAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await response.Content.ReadFromJsonAsync<MeResponse>(CancellationToken);
        Assert.Equal("mel@example.com", me?.Email);
        Assert.Equal("Test", me?.FirstName);
        Assert.False(me?.IsAdmin);
    }

    [Fact]
    public async Task GetMe_Anonymous_Returns401()
    {
        using var response = await GetMeAsync(Client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_AccessTokenOlderThan30Minutes_Returns401()
    {
        using var client = await CreateUserClientAsync();

        Clock.Advance(TimeSpan.FromMinutes(31));
        using var response = await GetMeAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_SecurityStampChanged_Returns401Immediately()
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        await WithUserAsync(
            "mel@example.com",
            (users, user) => users.UpdateSecurityStampAsync(user)
        );

        using var response = await GetMeAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_UserDeleted_Returns401Immediately()
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        await WithUserAsync("mel@example.com", (users, user) => users.DeleteAsync(user));

        using var response = await GetMeAsync(client);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }
}
