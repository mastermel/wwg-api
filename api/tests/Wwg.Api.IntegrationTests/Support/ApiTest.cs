using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wwg.Api.Data;

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
