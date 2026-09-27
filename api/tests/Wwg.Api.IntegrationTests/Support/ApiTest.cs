using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Auth;
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
