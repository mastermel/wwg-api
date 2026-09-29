using System.ComponentModel.DataAnnotations;

namespace Wwg.Api.Infrastructure.Backups;

/// <summary>
/// Database backups (<c>Backup</c> section). With no <see cref="Path"/>, no backups are made (with
/// a warning at startup).
/// </summary>
internal sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>
    /// The folder backups are written to. A relative path is resolved against the content root.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>How often a scheduled backup is made.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "7.00:00:00")]
    public TimeSpan Interval { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// How many backups of each kind (scheduled, before a migration) are kept; older ones are
    /// deleted.
    /// </summary>
    [Range(1, 1000)]
    public int Keep { get; set; } = 14;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Path);
}
