using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// An in-memory database with the real migrations applied, once per test run. Each test gets a
/// copy (SQLite's online backup takes milliseconds), so migrations are still exercised without
/// running them for every test.
/// </summary>
internal static class TemplateDatabase
{
    private static readonly Lazy<SqliteConnection> Template = new(Create);
    private static readonly Lock BackupLock = new();

    public static void CopyTo(SqliteConnection destination)
    {
        // A SqliteConnection isn't thread-safe, and tests run in parallel.
        lock (BackupLock)
        {
            Template.Value.BackupDatabase(destination);
        }
    }

    private static SqliteConnection Create()
    {
        // Kept open for the whole run: an in-memory database lives as long as its connection.
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        using var db = new WwgDbContext(
            new DbContextOptionsBuilder<WwgDbContext>().UseSqlite(connection).Options
        );
        db.Database.Migrate();

        return connection;
    }
}
