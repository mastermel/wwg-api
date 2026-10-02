using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// The Admin role follows Admin:Emails at startup. Each test starts a second host on the same
/// database with the setting, which is what a restart with new config does.
/// </summary>
public sealed class AdminSyncTests : ApiTest
{
    private WebApplicationFactory<Program> RestartWithAdmins(params string[] emails) =>
        App.WithWebHostBuilder(builder =>
        {
            // An empty list still has to replace any inherited value.
            builder.UseSetting("Admin:Emails", emails.Length == 0 ? "" : null);
            for (var i = 0; i < emails.Length; i++)
            {
                builder.UseSetting($"Admin:Emails:{i}", emails[i]);
            }
        });

    private static async Task<MeResponse?> SignInAndGetMeAsync(HttpClient client, string email)
    {
        using var login = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(email, TestPassword),
            TestContext.Current.CancellationToken
        );
        login.EnsureSuccessStatusCode();
        var token = await login.Content.ReadFromJsonAsync<TokenResponse>(
            TestContext.Current.CancellationToken
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token?.AccessToken
        );
        return await client.GetFromJsonAsync<MeResponse>(
            new Uri("/api/me", UriKind.Relative),
            TestContext.Current.CancellationToken
        );
    }

    /// <summary>Marks the account's email confirmed, as its welcome's link would.</summary>
    private Task<int> ConfirmAsync(string email) =>
        WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(
                u => u.Email == email,
                TestContext.Current.CancellationToken
            );
            user.EmailConfirmed = true;
            return await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        });

    [Fact]
    public async Task Startup_ListedButUnconfirmed_IsntMadeAdmin()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();

        using var restarted = RestartWithAdmins("mel@example.com");
        using var client = restarted.CreateClient();
        var me = await SignInAndGetMeAsync(client, "mel@example.com");

        Assert.False(me?.IsAdmin);
    }

    [Fact]
    public async Task Startup_EmailListedInAnyCase_MakesTheExistingAccountAdmin()
    {
        (await CreateUserClientAsync("mel@example.com")).Dispose();
        await ConfirmAsync("mel@example.com");

        using var restarted = RestartWithAdmins(" MEL@Example.com ");
        using var client = restarted.CreateClient();
        var me = await SignInAndGetMeAsync(client, "mel@example.com");

        Assert.True(me?.IsAdmin);
    }

    [Fact]
    public async Task Startup_EmailNoLongerListed_RemovesAdminAndEndsTheirSessions()
    {
        using var admin = await CreateAdminClientAsync("mel@example.com");

        using var restarted = RestartWithAdmins();
        using var oldToken = await restarted
            .CreateClient()
            .SendAsync(
                new HttpRequestMessage(HttpMethod.Get, "/api/me")
                {
                    Headers = { Authorization = admin.DefaultRequestHeaders.Authorization },
                },
                CancellationToken
            );
        var me = await SignInAndGetMeAsync(restarted.CreateClient(), "mel@example.com");

        Assert.Equal(HttpStatusCode.Unauthorized, oldToken.StatusCode);
        Assert.False(me?.IsAdmin);
    }

    [Fact]
    public async Task Register_ListedEmailAfterStartup_IsNotAdmin()
    {
        using var restarted = RestartWithAdmins("mel@example.com");
        using var client = restarted.CreateClient();
        using var register = await RegisterAsync(client, "mel@example.com");
        register.EnsureSuccessStatusCode();

        var me = await SignInAndGetMeAsync(client, "mel@example.com");

        Assert.False(me?.IsAdmin);
    }

    [Fact]
    public async Task Startup_ListedEmailWithNoAccount_StartsNormally()
    {
        using var restarted = RestartWithAdmins("nobody@example.com");

        using var response = await restarted
            .CreateClient()
            .GetAsync(new Uri("/health", UriKind.Relative), CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
