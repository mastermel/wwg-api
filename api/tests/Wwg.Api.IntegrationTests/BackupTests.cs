using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wwg.Api.Data;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class BackupTests : ApiTest
{
    // A real file: VACUUM INTO opens its target like the source, so from the tests' in-memory
    // database it would write another in-memory database, not a file.
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"wwg-backup-tests-{Guid.CreateVersion7()}"
    );

    private string Folder => Path.Combine(_root, "backups");

    private string ConnectionString => $"Data Source={Path.Combine(_root, "wwg.db")}";

    public BackupTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task Startup_PendingMigrations_BacksUpTheDatabaseBeforeMigrating()
    {
        await MigrateToAsync(ConnectionString, "AddCampaigns");

        await using (var app = WithBackups())
        {
            using var client = app.CreateClient();
        }

        var backup = Assert.Single(Directory.GetFiles(Folder, "wwg-*-before-migration.db"));
        Assert.Equal(
            "wwg-20260101-120000-before-migration.db",
            Path.GetFileName(backup),
            StringComparer.Ordinal
        );
        // The backup is the database as it was: only the first two migrations.
        Assert.Equal(2, await CountMigrationsAsync($"Data Source={backup}"));
        Assert.True(await CountMigrationsAsync(ConnectionString) > 2);
    }

    [Fact]
    public async Task Startup_NewDatabase_MakesNoBackupBeforeMigrating()
    {
        await using (var app = WithBackups())
        {
            using var client = app.CreateClient();
        }

        // The folder only exists once the scheduled backup (which may not have run yet) makes it.
        Assert.False(
            Directory.Exists(Folder)
                && Directory.GetFiles(Folder, "wwg-*-before-migration.db").Length > 0
        );
    }

    [Fact]
    public async Task Startup_NoBackupYet_BacksUpAtOnce()
    {
        await using var app = WithBackups();
        using var client = app.CreateClient();

        await WaitForBackupAsync("wwg-20260101-120000.db");
    }

    [Fact]
    public async Task Schedule_IntervalPasses_BacksUpAgain()
    {
        await using var app = WithBackups();
        using var client = app.CreateClient();
        await WaitForBackupAsync("wwg-20260101-120000.db");

        Clock.Advance(TimeSpan.FromDays(1));

        await WaitForBackupAsync("wwg-20260102-120000.db");
    }

    [Fact]
    public async Task Schedule_MoreThanKeep_DeletesTheOldest()
    {
        await using var app = WithBackups(keep: 2, interval: "01:00:00");
        using var client = app.CreateClient();
        await WaitForBackupAsync("wwg-20260101-120000.db");

        foreach (var hour in new[] { "13", "14" })
        {
            Clock.Advance(TimeSpan.FromHours(1));
            await WaitForBackupAsync($"wwg-20260101-{hour}0000.db");
        }

        Assert.Equal(
            "wwg-20260101-130000.db, wwg-20260101-140000.db",
            string.Join(
                ", ",
                Directory.GetFiles(Folder).Select(Path.GetFileName).Order(StringComparer.Ordinal)
            ),
            StringComparer.Ordinal
        );
    }

    private WebApplicationFactory<Program> WithBackups(
        int keep = 14,
        string interval = "1.00:00:00"
    ) =>
        App.WithWebHostBuilder(builder =>
            builder
                .UseSetting("ConnectionStrings:Default", ConnectionString)
                .UseSetting("Database:MigrateOnStartup", "true")
                .UseSetting("Backup:Path", Folder)
                .UseSetting("Backup:Keep", keep.ToString(CultureInfo.InvariantCulture))
                .UseSetting("Backup:Interval", interval)
        );

    private static async Task MigrateToAsync(string connectionString, string migration)
    {
        await using var db = new WwgDbContext(
            new DbContextOptionsBuilder<WwgDbContext>().UseSqlite(connectionString).Options
        );
        await db.GetService<IMigrator>().MigrateAsync(migration, CancellationToken);
    }

    private static async Task<int> CountMigrationsAsync(string connectionString)
    {
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory;";
        return Convert.ToInt32(
            await command.ExecuteScalarAsync(CancellationToken),
            CultureInfo.InvariantCulture
        );
    }

    /// <summary>Waits up to 5 seconds for the background service to write a backup.</summary>
    private async Task WaitForBackupAsync(string name)
    {
        var path = Path.Combine(Folder, name);
        for (
            var elapsed = TimeSpan.Zero;
            elapsed < TimeSpan.FromSeconds(5);
            elapsed += TimeSpan.FromMilliseconds(20)
        )
        {
            if (File.Exists(path))
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }

        throw new TimeoutException($"No backup {name} was made.");
    }

    protected override ValueTask DisposeTestAsync()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_root, recursive: true);

        return ValueTask.CompletedTask;
    }
}
