using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Infrastructure.Backups;

namespace Wwg.Api.Infrastructure;

internal static class DatabaseExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services)
    {
        services
            .AddValidatedOptions<DatabaseOptions>(DatabaseOptions.SectionName)
            .Configure<IConfiguration, IHostEnvironment>(
                (options, configuration, environment) =>
                    options.ConnectionString = ResolveDataSource(
                        configuration.GetConnectionString("Default"),
                        environment.ContentRootPath
                    )
            );

        services.AddValidatedOptions<BackupOptions>(BackupOptions.SectionName);
        services.AddSingleton<DatabaseBackup>();
        services.AddHostedService<BackupService>();

        services.AddSingleton<AuditInterceptor>();
        services.AddExceptionHandler<UniqueConstraintExceptionHandler>();
        services.AddDbContext<WwgDbContext>(
            (serviceProvider, options) =>
                options
                    .UseSqlite(
                        serviceProvider
                            .GetRequiredService<IOptions<DatabaseOptions>>()
                            .Value.ConnectionString
                    )
                    .AddInterceptors(serviceProvider.GetRequiredService<AuditInterceptor>())
        );

        // Queries a table rather than just opening a connection: SQLite creates a missing
        // database file on open, so that alone would pass even with no schema.
        services
            .AddHealthChecks()
            .AddDbContextCheck<WwgDbContext>(
                "database",
                customTestQuery: async (db, cancellationToken) =>
                {
                    await db.Users.AnyAsync(cancellationToken);
                    return true;
                }
            );

        return services;
    }

    /// <summary>
    /// Switches the database to WAL journal mode (better concurrent reads; the setting persists
    /// in the file), then applies pending migrations if <see cref="DatabaseOptions.MigrateOnStartup"/>
    /// is set. An existing database is backed up first (if backups are configured), so a migration
    /// that goes wrong can be undone; if that backup fails, startup stops before migrating.
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WwgDbContext>();
        await db.Database.ExecuteSqlAsync($"PRAGMA journal_mode = WAL;");

        if (!options.MigrateOnStartup)
        {
            return;
        }

        var backup = app.Services.GetRequiredService<DatabaseBackup>();
        if (
            backup.IsConfigured
            && (await db.Database.GetAppliedMigrationsAsync()).Any()
            && (await db.Database.GetPendingMigrationsAsync()).Any()
        )
        {
            await backup.CreateAsync(BackupKind.BeforeMigration, CancellationToken.None);
        }

        await db.Database.MigrateAsync();
    }

    private static string? ResolveDataSource(string? connectionString, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var builder = new SqliteConnectionStringBuilder(connectionString);
        var isFile =
            builder.Mode != SqliteOpenMode.Memory
            && builder.DataSource is not ("" or ":memory:")
            && !builder.DataSource.StartsWith("file:", StringComparison.Ordinal);
        if (isFile && !Path.IsPathRooted(builder.DataSource))
        {
            builder.DataSource = Path.Combine(contentRoot, builder.DataSource);
        }

        return builder.ToString();
    }
}
