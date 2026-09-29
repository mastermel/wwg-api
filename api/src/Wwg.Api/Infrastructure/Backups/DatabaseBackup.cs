using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;

namespace Wwg.Api.Infrastructure.Backups;

internal enum BackupKind
{
    Scheduled,
    BeforeMigration,
}

/// <summary>
/// Copies the database to a file in the backup folder with <c>VACUUM INTO</c>, which takes a
/// consistent snapshot while the app keeps running (WAL included), then deletes the oldest backups
/// of that kind beyond <see cref="BackupOptions.Keep"/>.
/// </summary>
/// <remarks>
/// Files are named <c>wwg-{yyyyMMdd-HHmmss}.db</c> (UTC), with <c>-before-migration</c> before the
/// extension for the snapshot taken before startup migrations. The time in the name, not the
/// file's, decides which are newest, so it follows the injected <see cref="TimeProvider"/>.
/// </remarks>
internal sealed partial class DatabaseBackup(
    IServiceScopeFactory scopes,
    IOptions<BackupOptions> options,
    IHostEnvironment environment,
    TimeProvider time,
    ILogger<DatabaseBackup> logger
)
{
    private const string TimestampFormat = "yyyyMMdd-HHmmss";
    private const string Prefix = "wwg-";
    private const string BeforeMigrationSuffix = "-before-migration";
    private const string Extension = ".db";

    public bool IsConfigured => options.Value.IsConfigured;

    private string Folder =>
        System.IO.Path.Combine(environment.ContentRootPath, options.Value.Path ?? "");

    /// <summary>Makes a backup, and returns its path.</summary>
    public async Task<string> CreateAsync(BackupKind kind, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Backup:Path isn't set.");
        }

        Directory.CreateDirectory(Folder);
        var path = System.IO.Path.Combine(
            Folder,
            Prefix
                + time.GetUtcNow().ToString(TimestampFormat, CultureInfo.InvariantCulture)
                + (kind == BackupKind.BeforeMigration ? BeforeMigrationSuffix : "")
                + Extension
        );
        // Written under another name and then renamed, so a backup cut short (a crash, a full
        // disk) is never mistaken for a good one.
        var partial = path + ".partial";
        File.Delete(partial);

        await using (var scope = scopes.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<WwgDbContext>();
            await db.Database.ExecuteSqlAsync($"VACUUM INTO {partial}", cancellationToken);
        }

        File.Move(partial, path, overwrite: true);
        LogCreated(logger, path);

        foreach (var old in Backups(kind).Skip(options.Value.Keep))
        {
            File.Delete(old.Path);
            LogDeleted(logger, old.Path);
        }

        return path;
    }

    /// <summary>When the newest backup of <paramref name="kind"/> was made, if there is one.</summary>
    public DateTimeOffset? Latest(BackupKind kind) =>
        Backups(kind).Select(b => (DateTimeOffset?)b.CreatedAt).FirstOrDefault();

    /// <summary>The backups of <paramref name="kind"/>, newest first.</summary>
    private List<(string Path, DateTimeOffset CreatedAt)> Backups(BackupKind kind)
    {
        var found = new List<(string Path, DateTimeOffset CreatedAt)>();
        if (!Directory.Exists(Folder))
        {
            return found;
        }

        var suffix = kind == BackupKind.BeforeMigration ? BeforeMigrationSuffix : "";
        foreach (var path in Directory.EnumerateFiles(Folder, Prefix + "*" + suffix + Extension))
        {
            // "wwg-*.db" also matches the before-migration backups; their stamp doesn't parse.
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (
                DateTimeOffset.TryParseExact(
                    name[Prefix.Length..^suffix.Length],
                    TimestampFormat,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var createdAt
                )
            )
            {
                found.Add((path, createdAt));
            }
        }

        return [.. found.OrderByDescending(b => b.CreatedAt)];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Backed up the database to {Path}")]
    private static partial void LogCreated(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleted the old backup {Path}")]
    private static partial void LogDeleted(ILogger logger, string path);
}
