using System.Globalization;
using System.Net;
using Microsoft.Data.Sqlite;

namespace Wwg.Api.IntegrationTests;

public sealed class DatabaseTests
{
    [Fact]
    public async Task Startup_Default_AppliesMigrationsInWalMode()
    {
        await using var factory = new WwgApiFactory();
        using var client = factory.CreateClient();

        await using var connection = new SqliteConnection(factory.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        Assert.Equal("wal", await ScalarAsync(connection, "PRAGMA journal_mode;"));
        var migrations = Convert.ToInt32(
            await ScalarAsync(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory;"),
            CultureInfo.InvariantCulture
        );
        Assert.True(migrations > 0);
    }

    [Fact]
    public async Task GetHealth_DatabaseNotMigrated_ReturnsServiceUnavailable()
    {
        await using var factory = new WwgApiFactory();
        using var client = factory
            .WithWebHostBuilder(builder => builder.UseSetting("Database:MigrateOnStartup", "false"))
            .CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health", UriKind.Relative),
            TestContext.Current.CancellationToken
        );

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains(
            "Unhealthy",
            await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken),
            StringComparison.Ordinal
        );
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }
}
