using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Wwg.Api.Infrastructure.Email;

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

        // HTTPS, so the client's cookie container sends the Secure refresh-token cookie.
        ClientOptions.BaseAddress = new Uri("https://localhost");
    }

    public string ConnectionString { get; }

    /// <summary>Every email the app sent.</summary>
    internal FakeEmailService Emails { get; } = new();

    /// <summary>The app's clock. Starts at a fixed time; move it with <c>Advance</c>.</summary>
    public FakeTimeProvider Clock { get; } =
        new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseSetting("ConnectionStrings:Default", ConnectionString)
            // Already migrated: it's a copy of the template.
            .UseSetting("Database:MigrateOnStartup", "false")
            // Tests make many auth requests from one "IP"; RateLimitingTests covers the limits.
            .UseSetting("RateLimits:Auth:PermitLimit", "100000")
            .UseSetting("RateLimits:Refresh:PermitLimit", "100000")
            .UseSetting("RateLimits:Email:PermitLimit", "100000")
            .ConfigureTestServices(services =>
            {
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IEmailService>(Emails);
                // In-memory keys: tests never write key files, and tokens don't outlive the test.
                services.PostConfigure<KeyManagementOptions>(options =>
                    options.XmlRepository = new InMemoryXmlRepository()
                );
                // Identity's 100,000 PBKDF2 iterations are deliberate in production; tests create
                // many users, so one iteration here.
                services.Configure<PasswordHasherOptions>(options => options.IterationCount = 1);
            });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
