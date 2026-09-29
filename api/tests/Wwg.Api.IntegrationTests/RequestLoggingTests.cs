using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class RequestLoggingTests : ApiTest
{
    private readonly CapturingLoggerProvider _logs = new();

    [Fact]
    public async Task ApiRequest_Always_LogsMethodPathAndStatus()
    {
        using var client = CreateLoggingClient();

        using var response = await client.GetAsync(
            new Uri("/api/no-such-thing?code=secret", UriKind.Relative),
            CancellationToken
        );

        var line = Assert.Single(_logs.RequestLines);
        Assert.Contains("Method: GET", line, StringComparison.Ordinal);
        Assert.Contains("Path: /api/no-such-thing", line, StringComparison.Ordinal);
        Assert.Contains("StatusCode: 404", line, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", line, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HealthCheck_Always_IsNotLogged()
    {
        using var client = CreateLoggingClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            CancellationToken
        );

        response.EnsureSuccessStatusCode();
        Assert.Empty(_logs.RequestLines);
    }

    private HttpClient CreateLoggingClient() =>
        App.WithWebHostBuilder(builder =>
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<ILoggerProvider>(_logs)
                )
            )
            .CreateClient();

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<(string Category, string Message)> _entries = new();

        public IEnumerable<string> RequestLines =>
            _entries
                .Where(e =>
                    e.Category.StartsWith(
                        "Microsoft.AspNetCore.HttpLogging",
                        StringComparison.Ordinal
                    )
                )
                .Select(e => e.Message);

        public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

        public void Dispose() { }

        private sealed class Logger(
            string category,
            ConcurrentQueue<(string Category, string Message)> entries
        ) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state)
                where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter
            ) => entries.Enqueue((category, formatter(state, exception)));
        }
    }
}
