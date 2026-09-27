using System.Globalization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class DatabaseTests : ApiTest
{
    [Fact]
    public async Task Model_Always_HasNoChangesMissingAMigration()
    {
        var pending = await WithDbAsync(db =>
            Task.FromResult(db.Database.HasPendingModelChanges())
        );

        Assert.False(
            pending,
            "The model has changes with no migration. Run: dotnet ef migrations add <Name> "
                + "--project api/src/Wwg.Api --output-dir Data/Migrations"
        );
    }

    [Fact]
    public async Task NewTest_Always_StartsWithAnEmptyDatabase()
    {
        // Other tests (e.g. AuditTests) add users; each test must get its own copy.
        var users = await WithDbAsync(db => db.Users.CountAsync(CancellationToken));

        Assert.Equal(0, users);
    }

    [Fact]
    public async Task Startup_FileDatabase_AppliesMigrationsInWalMode()
    {
        // WAL needs a real file (in-memory databases use "memory" journaling).
        var path = Path.Combine(Path.GetTempPath(), $"wwg-tests-{Guid.CreateVersion7()}.db");
        var connectionString = $"Data Source={path}";
        try
        {
            await using (
                var app = App.WithWebHostBuilder(builder =>
                    builder
                        .UseSetting("ConnectionStrings:Default", connectionString)
                        .UseSetting("Database:MigrateOnStartup", "true")
                )
            )
            {
                using var client = app.CreateClient();
            }

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(CancellationToken);
            Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode;"));
            var migrations = Convert.ToInt32(
                await ScalarAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;"),
                CultureInfo.InvariantCulture
            );
            Assert.True(migrations > 0);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(path + suffix);
            }
        }
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(CancellationToken);
    }
}
