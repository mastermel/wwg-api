using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Auth;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Sides;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// Base class for API tests. xUnit creates a new instance per test, so every test gets its own
/// app, database and clock; tests share nothing and can run in parallel.
/// </summary>
public abstract class ApiTest : IAsyncDisposable
{
    private HttpClient? _client;

    protected WwgApiFactory App { get; } = new();

    /// <summary>An anonymous client for the app.</summary>
    protected HttpClient Client => _client ??= App.CreateClient();

    protected FakeTimeProvider Clock => App.Clock;

    private protected FakeEmailService Emails => App.Emails;

    protected static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>A password every test user can share (cheap to hash in tests).</summary>
    protected const string TestPassword = "correct horse battery";

    private int _userCount;

    /// <summary>A new client with its own cookie jar (so its own refresh cookie).</summary>
    protected HttpClient CreateClient() => App.CreateClient();

    /// <summary>Registers through the real endpoint.</summary>
    protected static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password = TestPassword,
        string firstName = "Test",
        string lastName = "User"
    ) =>
        client.PostAsJsonAsync(
            new Uri("/api/auth/register", UriKind.Relative),
            new RegisterRequest(email, password, firstName, lastName),
            CancellationToken
        );

    /// <summary>
    /// Registers a new user (a unique email unless given) and returns a client signed in as them:
    /// bearer token set, refresh cookie in its cookie jar.
    /// </summary>
    protected async Task<HttpClient> CreateUserClientAsync(string? email = null)
    {
        email ??= $"user{Interlocked.Increment(ref _userCount)}@example.com";
        var client = CreateClient();
        using var response = await RegisterAsync(client, email);
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token?.AccessToken
        );
        return client;
    }

    /// <summary>
    /// A signed-in Manager (decision 0015): registers, and is granted the role directly, as an
    /// Admin would on the user's page. No sign-in again is needed: the role is read from the
    /// database.
    /// </summary>
    protected async Task<HttpClient> CreateManagerClientAsync(string email = "manager@example.com")
    {
        var client = await CreateUserClientAsync(email);
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user =
            await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException("The manager didn't register.");
        (await users.AddToRoleAsync(user, Roles.Manager)).EnsureSucceeded();
        return client;
    }

    /// <summary>
    /// A signed-in Admin. Registers, grants the role directly (what the startup sync does for a
    /// listed email), then signs in again so the token carries the role.
    /// </summary>
    protected async Task<HttpClient> CreateAdminClientAsync(string email = "admin@example.com")
    {
        (await CreateUserClientAsync(email)).Dispose();
        await using (var scope = App.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            var user =
                await users.FindByEmailAsync(email)
                ?? throw new InvalidOperationException("The admin didn't register.");
            (await users.AddToRoleAsync(user, Roles.Admin)).EnsureSucceeded();
        }

        var client = CreateClient();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/auth/login", UriKind.Relative),
            new LoginRequest(email, TestPassword),
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token?.AccessToken
        );
        return client;
    }

    /// <summary>
    /// A campaign created by its Umpire through the API, with two Players who joined with the join
    /// link: one commands the army "First Corps" (in the side "Coalition", with one unit,
    /// "1st Division"), the other
    /// commands nothing. Plus an Admin and a
    /// signed-in outsider.
    /// </summary>
    private protected async Task<CampaignScenario> CreateCampaignScenarioAsync(
        string name = "The Peninsular War"
    )
    {
        var admin = await CreateAdminClientAsync("admin@example.com");
        var umpire = await CreateUserClientAsync("umpire@example.com");
        var commander = await CreateUserClientAsync("commander@example.com");
        var player = await CreateUserClientAsync("player@example.com");
        var outsider = await CreateUserClientAsync("outsider@example.com");

        using var created = await umpire.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new CreateCampaignRequest(name, "A campaign for testing."),
            CancellationToken
        );
        created.EnsureSuccessStatusCode();
        var campaign = await created.Content.ReadAsAsync<CampaignResponse>();
        var campaignId = campaign?.Id ?? throw new InvalidOperationException("No campaign.");

        await JoinAsync(umpire, campaignId, commander, player);

        var memberIds = await MemberIdsAsync(campaignId);

        var sideId = await PostForIdAsync<SideResponse>(
            umpire,
            $"/api/campaigns/{campaignId}/sides",
            new CreateSideRequest("Coalition"),
            f => f.Id
        );
        var armyId = await PostForIdAsync<ArmyResponse>(
            umpire,
            $"/api/campaigns/{campaignId}/armies",
            new CreateArmyRequest("First Corps", memberIds["COMMANDER@EXAMPLE.COM"], sideId),
            a => a.Id
        );
        var unitId = await PostForIdAsync<ArmyUnitResponse>(
            umpire,
            $"/api/armies/{armyId}/units",
            new CreateArmyUnitRequest("1st Division", UnitType.LineInfantry, 5, 20),
            u => u.Id
        );

        return new CampaignScenario(
            campaignId,
            memberIds["UMPIRE@EXAMPLE.COM"],
            memberIds["COMMANDER@EXAMPLE.COM"],
            memberIds["PLAYER@EXAMPLE.COM"],
            sideId,
            armyId,
            unitId,
            new Dictionary<Role, HttpClient>
            {
                [Role.Admin] = admin,
                [Role.Umpire] = umpire,
                [Role.Commander] = commander,
                [Role.Player] = player,
                [Role.NonMember] = outsider,
            }
        );
    }

    /// <summary>The campaign's membership IDs, by the member's normalized (upper-case) email.</summary>
    private Task<Dictionary<string, Guid>> MemberIdsAsync(Guid campaignId) =>
        WithDbAsync(db =>
            Task.FromResult(
                db.CampaignMembers.Where(m => m.CampaignId == campaignId)
                    .Select(m => new { m.Id, Email = m.User.NormalizedEmail })
                    .ToDictionary(m => m.Email ?? "", m => m.Id, StringComparer.Ordinal)
            )
        );

    /// <summary>POSTs <paramref name="body"/>, expects success, and returns the new thing's ID.</summary>
    private static async Task<Guid> PostForIdAsync<TResponse>(
        HttpClient client,
        string path,
        object body,
        Func<TResponse, Guid> id
    )
        where TResponse : class
    {
        using var response = await client.PostAsJsonAsync(
            new Uri(path, UriKind.Relative),
            body,
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
        var created =
            await response.Content.ReadAsAsync<TResponse>()
            ?? throw new InvalidOperationException($"POST {path} returned nothing.");
        return id(created);
    }

    /// <summary>The <paramref name="joiners"/> join the campaign with its join link.</summary>
    private static async Task JoinAsync(
        HttpClient umpire,
        Guid campaignId,
        params HttpClient[] joiners
    )
    {
        var joinCode = await umpire.GetAsAsync<JoinCodeResponse>(
            $"/api/campaigns/{campaignId}/join-code"
        );
        foreach (var joiner in joiners)
        {
            using var joined = await joiner.PostAsync(
                new Uri($"/api/join/{joinCode?.JoinCode}", UriKind.Relative),
                null,
                CancellationToken
            );
            joined.EnsureSuccessStatusCode();
        }
    }

    /// <summary>Runs <paramref name="action"/> with a fresh DbContext, for seeding and checking data.</summary>
    private protected async Task<T> WithDbAsync<T>(Func<WwgDbContext, Task<T>> action)
    {
        await using var scope = App.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<WwgDbContext>());
    }

    public async ValueTask DisposeAsync()
    {
        _client?.Dispose();
        await App.DisposeAsync();
        await DisposeTestAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>Extra cleanup for a test class (e.g. temporary files), after the app stops.</summary>
    protected virtual ValueTask DisposeTestAsync() => ValueTask.CompletedTask;
}
