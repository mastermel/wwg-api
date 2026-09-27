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

        return services;
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
