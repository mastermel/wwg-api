using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Auth;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Admins masquerading as other users (decision 0012).</summary>
public sealed class MasqueradeTests : ApiTest
{
    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string path) =>
        client.PostAsync(new Uri(path, UriKind.Relative), null, CancellationToken);

    /// <summary>Uses the tokens in <paramref name="response"/> (its cookie is in the jar already).</summary>
    private static async Task<TokenResponse> UseTokenAsync(
        HttpClient client,
        HttpResponseMessage response
    )
    {
        response.EnsureSuccessStatusCode();
        var token =
            await response.Content.ReadAsAsync<TokenResponse>()
            ?? throw new InvalidOperationException("No token.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token.AccessToken
        );
        return token;
    }

    private static async Task<MeResponse> MeAsync(HttpClient client) =>
        await client.GetAsAsync<MeResponse>("/api/me")
        ?? throw new InvalidOperationException("No user.");

    /// <summary>An Admin, and Bob (a user), with the Admin's client now masquerading as Bob.</summary>
    private async Task<(HttpClient Admin, Guid BobId)> MasqueradingAsync()
    {
        var admin = await CreateAdminClientAsync();
        using var bob = await CreateUserClientAsync("bob@example.com");
        var bobId = (await MeAsync(bob)).Id;
        using var started = await PostAsync(admin, $"/api/admin/users/{bobId}/masquerade");
        await UseTokenAsync(admin, started);
        return (admin, bobId);
    }

    [Fact]
    public async Task Masquerade_AsAUser_TheSessionIsTheirsAndSaysSo()
    {
        var (admin, bobId) = await MasqueradingAsync();
        using var _ = admin;

        var me = await MeAsync(admin);

        Assert.Equal((bobId, "bob@example.com", false), (me.Id, me.Email, me.IsAdmin));
        Assert.Equal("Test User", me.Masquerade?.AdminName);
        // Kept to the second.
        var expected = Clock.GetUtcNow().UtcDateTime.AddHours(8);
        Assert.InRange((expected - me.Masquerade!.EndsAt).TotalSeconds, 0, 1); // Checked just above.
    }

    [Fact]
    public async Task Masquerade_AsAUser_HasNoMoreThanTheirPermissions()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var admin = scenario.As(Role.Admin);
        var player = await MeAsync(scenario.As(Role.Player));
        using var started = await PostAsync(admin, $"/api/admin/users/{player.Id}/masquerade");
        await UseTokenAsync(admin, started);

        using var adminPage = await admin.GetAsync(
            new Uri("/api/admin/users", UriKind.Relative),
            CancellationToken
        );
        using var start = await PostAsync(admin, $"/api/campaigns/{scenario.CampaignId}/start");

        Assert.Equal(HttpStatusCode.Forbidden, adminPage.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    [Fact]
    public async Task Masquerade_ByANonAdmin_Returns403()
    {
        using var user = await CreateUserClientAsync();
        using var bob = await CreateUserClientAsync("bob@example.com");
        var bobId = (await MeAsync(bob)).Id;

        using var response = await PostAsync(user, $"/api/admin/users/{bobId}/masquerade");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Masquerade_AsYourself_Returns409()
    {
        using var admin = await CreateAdminClientAsync();
        var adminId = (await MeAsync(admin)).Id;

        using var response = await PostAsync(admin, $"/api/admin/users/{adminId}/masquerade");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Masquerade_UnknownUser_Returns404()
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await PostAsync(
            admin,
            $"/api/admin/users/{Guid.CreateVersion7()}/masquerade"
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Masquerade_WhileMasqueradingAsAnotherAdmin_Returns409()
    {
        using var admin = await CreateAdminClientAsync();
        using var other = await CreateAdminClientAsync("other-admin@example.com");
        using var bob = await CreateUserClientAsync("bob@example.com");
        var otherId = (await MeAsync(other)).Id;
        var bobId = (await MeAsync(bob)).Id;
        using var started = await PostAsync(admin, $"/api/admin/users/{otherId}/masquerade");
        await UseTokenAsync(admin, started);

        using var again = await PostAsync(admin, $"/api/admin/users/{bobId}/masquerade");

        await again.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Refresh_WhileMasquerading_GoesOnMasquerading()
    {
        var (admin, bobId) = await MasqueradingAsync();
        using var _ = admin;

        using var refreshed = await PostAsync(admin, "/api/auth/refresh");
        await UseTokenAsync(admin, refreshed);

        var me = await MeAsync(admin);
        Assert.Equal(bobId, me.Id);
        Assert.NotNull(me.Masquerade);
    }

    [Fact]
    public async Task Refresh_NearTheEnd_GivesAnAccessTokenThatEndsWithIt()
    {
        var (admin, _) = await MasqueradingAsync();
        using var client = admin;
        Clock.Advance(TimeSpan.FromHours(8) - TimeSpan.FromMinutes(10));

        using var refreshed = await PostAsync(client, "/api/auth/refresh");

        var token = await UseTokenAsync(client, refreshed);
        Assert.InRange(token.ExpiresIn, 599, 600);
    }

    [Fact]
    public async Task Refresh_AfterEightHours_Returns401()
    {
        var (admin, _) = await MasqueradingAsync();
        using var client = admin;
        Clock.Advance(TimeSpan.FromHours(8) + TimeSpan.FromSeconds(1));

        using var refreshed = await PostAsync(client, "/api/auth/refresh");

        await refreshed.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterTheAdminLostTheRole_Returns401()
    {
        var (admin, _) = await MasqueradingAsync();
        using var client = admin;
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByEmailAsync("admin@example.com");
            (await users.RemoveFromRoleAsync(user!, Roles.Admin)).EnsureSucceeded(); // Registered above.
        }

        using var refreshed = await PostAsync(client, "/api/auth/refresh");

        await refreshed.AssertProblemAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EndMasquerade_ReturnsToTheAdminsOwnSession()
    {
        var (admin, _) = await MasqueradingAsync();
        using var client = admin;

        using var ended = await PostAsync(client, "/api/auth/masquerade/end");
        await UseTokenAsync(client, ended);

        var me = await MeAsync(client);
        Assert.Equal(("admin@example.com", true, null), (me.Email, me.IsAdmin, me.Masquerade));
        // The cookie is the Admin's too.
        using var refreshed = await PostAsync(client, "/api/auth/refresh");
        await UseTokenAsync(client, refreshed);
        Assert.Null((await MeAsync(client)).Masquerade);
    }

    [Fact]
    public async Task EndMasquerade_NotMasquerading_Returns409()
    {
        using var admin = await CreateAdminClientAsync();

        using var response = await PostAsync(admin, "/api/auth/masquerade/end");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task EndMasquerade_SignedOut_Returns401()
    {
        using var response = await PostAsync(Client, "/api/auth/masquerade/end");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WhileMasquerading_StaysAMasquerade()
    {
        var (admin, _) = await MasqueradingAsync();
        using var client = admin;

        // Every test user has the same password, so here the Admin knows it.
        using var changed = await client.PutAsJsonAsync(
            new Uri("/api/me/password", UriKind.Relative),
            new ChangePasswordRequest(TestPassword, "a brand new passphrase"),
            CancellationToken
        );
        await UseTokenAsync(client, changed);

        Assert.NotNull((await MeAsync(client)).Masquerade);
    }
}
