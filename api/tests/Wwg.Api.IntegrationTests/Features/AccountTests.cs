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
    public async Task UpdateMe_TooLongName_IsAValidationError()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me",
            new UpdateProfileRequest("Melanie", new string('x', 101))
        );

        await response.AssertValidationProblemAsync("lastName");
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

    [Theory]
    [InlineData("/api/me/password")]
    [InlineData("/api/me/email")]
    public async Task AccountChange_FiveWrongCurrentPasswords_LocksTheAccount(string path)
    {
        using var client = await CreateUserClientAsync("mel@example.com");
        object Body(string password) =>
            path.EndsWith("/email", StringComparison.Ordinal)
                ? new ChangeEmailRequest("new@example.com", password)
                : new ChangePasswordRequest(password, "a brand new password");
        for (var i = 0; i < 5; i++)
        {
            using var wrong = await PutAsync(client, path, Body("not my password"));
        }

        using var response = await PutAsync(client, path, Body(TestPassword));

        var problem = await response.AssertValidationProblemAsync("currentPassword");
        Assert.StartsWith(
            "Too many wrong passwords",
            problem.Errors["currentPassword"][0],
            StringComparison.Ordinal
        );
        // The same lockout as sign-in: guessing here locks signing in too.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await LoginStatusAsync("mel@example.com", TestPassword)
        );
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

    private async Task<HttpClient> SignInOnAnotherDeviceAsync(string email)
    {
        var client = CreateClient();
        using var login = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(email, TestPassword),
            CancellationToken
        );
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token?.AccessToken);
        return client;
    }

    [Fact]
    public async Task ChangeEmail_Valid_ChangesWhatTheAccountSignsInWith()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await PutAsync(
            client,
            "/api/me/email",
            new ChangeEmailRequest(" melanie@example.com ", TestPassword)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new("Bearer", token?.AccessToken);
        var me = await client.GetFromJsonAsync<MeResponse>(
            new Uri("/api/me", UriKind.Relative),
            CancellationToken
        );
        Assert.Equal("melanie@example.com", me?.Email);
        Assert.Equal(
            HttpStatusCode.OK,
            await LoginStatusAsync("melanie@example.com", TestPassword)
        );
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            await LoginStatusAsync("mel@example.com", TestPassword)
        );
    }

    [Fact]
    public async Task ChangeEmail_Valid_SendsANoticeToTheOldAddress()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        (
            await PutAsync(
                client,
                "/api/me/email",
                new ChangeEmailRequest("melanie@example.com", TestPassword)
            )
        ).EnsureSuccessStatusCode();

        var notice = await Emails.WaitForEmailToAsync("mel@example.com");
        Assert.Contains("melanie@example.com", notice.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangeEmail_Valid_SignsOutOtherSessions()
    {
        using var thisDevice = await CreateUserClientAsync("mel@example.com");
        using var otherDevice = await SignInOnAnotherDeviceAsync("mel@example.com");

        (
            await PutAsync(
                thisDevice,
                "/api/me/email",
                new ChangeEmailRequest("melanie@example.com", TestPassword)
            )
        ).EnsureSuccessStatusCode();
        using var me = await GetMeAsync(otherDevice);

        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task ChangeEmail_WrongPassword_IsAnErrorOnCurrentPassword()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me/email",
            new ChangeEmailRequest("new@example.com", "not my password")
        );

        await response.AssertValidationProblemAsync("currentPassword");
    }

    [Fact]
    public async Task ChangeEmail_SameEmailInAnotherCase_IsAnErrorOnNewEmail()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await PutAsync(
            client,
            "/api/me/email",
            new ChangeEmailRequest("MEL@example.com", TestPassword)
        );

        await response.AssertValidationProblemAsync("newEmail");
    }

    [Fact]
    public async Task ChangeEmail_UsedByAnotherAccount_Returns409AndChangesNothing()
    {
        (await CreateUserClientAsync("taken@example.com")).Dispose();
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await PutAsync(
            client,
            "/api/me/email",
            new ChangeEmailRequest("Taken@example.com", TestPassword)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, await LoginStatusAsync("mel@example.com", TestPassword));
    }

    [Fact]
    public async Task ChangeEmail_NotAnEmail_IsAValidationError()
    {
        using var client = await CreateUserClientAsync();

        using var response = await PutAsync(
            client,
            "/api/me/email",
            new ChangeEmailRequest("nope", TestPassword)
        );

        await response.AssertValidationProblemAsync("newEmail");
    }

    [Fact]
    public async Task SignOutEverywhere_EndsEverySessionIncludingThisOne()
    {
        using var thisDevice = await CreateUserClientAsync("mel@example.com");
        using var otherDevice = await SignInOnAnotherDeviceAsync("mel@example.com");

        using var response = await thisDevice.PostAsync(
            new Uri("/api/me/sign-out-everywhere", UriKind.Relative),
            null,
            CancellationToken
        );
        using var thisMe = await GetMeAsync(thisDevice);
        using var otherMe = await GetMeAsync(otherDevice);
        using var otherRefresh = await otherDevice.PostAsync(
            new Uri("/api/auth/refresh", UriKind.Relative),
            null,
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, thisMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherMe.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, otherRefresh.StatusCode);
    }
}
