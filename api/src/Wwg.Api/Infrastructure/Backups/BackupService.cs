using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure.Backups;

/// <summary>
/// Makes a scheduled backup every <see cref="BackupOptions.Interval"/>. The schedule follows the
/// newest backup on disk, so a restart doesn't reset it: a backup is made at startup only if the
/// last one is due.
/// </summary>
internal sealed partial class BackupService(
    DatabaseBackup backup,
    IOptions<BackupOptions> options,
    TimeProvider time,
    ILogger<BackupService> logger
) : BackgroundService
{
    /// <summary>After a failed backup, how long until the next try (at most the interval).</summary>
    private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!backup.IsConfigured)
        {
            LogNotConfigured(logger);
            return;
        }

        var interval = options.Value.Interval;
        while (!stoppingToken.IsCancellationRequested)
        {
            var due = (backup.Latest(BackupKind.Scheduled) + interval) ?? time.GetUtcNow();
            var wait = due - time.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, stoppingToken);
            }

            try
            {
                await backup.CreateAsync(BackupKind.Scheduled, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogFailed(logger, exception);
                await Task.Delay(
                    interval < RetryAfterFailure ? interval : RetryAfterFailure,
                    time,
                    stoppingToken
                );
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No backup folder configured (Backup:Path): the database won't be backed up"
    )]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "The scheduled database backup failed")]
    private static partial void LogFailed(ILogger logger, Exception exception);
}
