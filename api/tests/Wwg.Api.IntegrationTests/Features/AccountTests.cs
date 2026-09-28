using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Auth;
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

    private static Task<HttpResponseMessage> PutAsync<T>(HttpClient client, string path, T body) =>
        client.PutAsJsonAsync(new Uri(path, UriKind.Relative), body, CancellationToken);

    private async Task<HttpStatusCode> LoginStatusAsync(string email, string password)
    {
        using var response = await CreateClient()
            .PostAsJsonAsync(
                new Uri("/api/auth/login", UriKind.Relative),
                new LoginRequest(email, password),
                CancellationToken
            );
        return response.StatusCode;
    }

    [Fact]
    public async Task UpdateMe_ValidNames_SavesThemTrimmed()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await PutAsync(
            client,
            "/api/me",
            new UpdateProfileRequest(" Melanie ", " Grey ")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<MeResponse>(
            new Uri("/api/me", UriKind.Relative),
            CancellationToken
        );
        Assert.Equal(("Melanie", "Grey"), (me?.FirstName, me?.LastName));
    }

    [Fact]
    public async Task UpdateMe_BlankName_IsAValidationError()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me",
            new UpdateProfileRequest("   ", "Grey")
        );

        await response.AssertValidationProblemAsync("firstName");
    }

    [Fact]
    public async Task ChangePassword_CorrectCurrentPassword_ChangesItAndKeepsThisSession()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await PutAsync(
            client,
            "/api/me/password",
            new ChangePasswordRequest(TestPassword, "a brand new password")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token?.AccessToken);
        using var me = await GetMeAsync(client);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(
            HttpStatusCode.OK,
            await LoginStatusAsync("mel@example.com", "a brand new password")
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await LoginStatusAsync("mel@example.com", TestPassword)
        );
    }

    [Fact]
    public async Task ChangePassword_Succeeded_SignsOutOtherSessions()
    {
        using var thisDevice = await CreateUserClientAsync("mel@example.com");
        var otherDevice = CreateClient();
        using var login = await otherDevice.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest("mel@example.com", TestPassword),
            CancellationToken
        );
        var otherToken = await login.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        otherDevice.DefaultRequestHeaders.Authorization = new("Bearer", otherToken?.AccessToken);

        (
            await PutAsync(
                thisDevice,
                "/api/me/password",
                new ChangePasswordRequest(TestPassword, "a brand new password")
            )
        ).EnsureSuccessStatusCode();
        using var me = await GetMeAsync(otherDevice);
        using var refresh = await otherDevice.PostAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            null,
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsAnErrorOnCurrentPassword()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me/password",
            new ChangePasswordRequest("not my password", "a brand new password")
        );

        await response.AssertValidationProblemAsync("currentPassword");
    }

    [Fact]
    public async Task ChangePassword_TooShortNewPassword_IsAnErrorOnNewPassword()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me/password",
            new ChangePasswordRequest(TestPassword, "short")
        );

        await response.AssertValidationProblemAsync("newPassword");
    }
}
