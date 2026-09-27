using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;

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
    /// is set.
    /// </summary>
    public static void InitializeDatabase(this WebApplication app)
    {
        var options = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WwgDbContext>();
        db.Database.ExecuteSql($"PRAGMA journal_mode = WAL;");

        if (options.MigrateOnStartup)
        {
            db.Database.Migrate();
        }
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
