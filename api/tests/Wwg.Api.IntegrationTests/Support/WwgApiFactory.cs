using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// Hosts the real API in-process with its own fresh database (a copy of the migrated template)
/// and a fake clock.
/// </summary>
/// <remarks>
/// The database is a named, shared-cache in-memory SQLite database. The app connects to it
/// through its normal connection string, and this factory holds one connection open, which keeps
/// it alive until the factory is disposed.
/// </remarks>
public sealed class WwgApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _database;

    public WwgApiFactory()
    {
        ConnectionString =
            $"Data Source=wwg-test-{Guid.CreateVersion7():N};Mode=Memory;Cache=Shared";
        _database = new SqliteConnection(ConnectionString);
        _database.Open();
        TemplateDatabase.CopyTo(_database);
    }

    public string ConnectionString { get; }

    /// <summary>The app's clock. Starts at a fixed time; move it with <c>Advance</c>.</summary>
    public FakeTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("ConnectionStrings:Default", ConnectionString)
            // Already migrated: it's a copy of the template.
            .UseSetting("Database:MigrateOnStartup", "false")
            .ConfigureTestServices(services => services.AddSingleton<TimeProvider>(Clock));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
