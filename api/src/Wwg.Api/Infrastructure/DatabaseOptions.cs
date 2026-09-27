using System.ComponentModel.DataAnnotations;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Database settings (<c>Database</c> section), plus the SQLite connection string, which comes
/// from the standard <c>ConnectionStrings:Default</c>.
/// </summary>
internal sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// From <c>ConnectionStrings:Default</c>. A relative file path is resolved against the content
    /// root, so it doesn't depend on the working directory.
    /// </summary>
    [Required(ErrorMessage = "ConnectionStrings:Default is required.")]
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Apply pending migrations at startup (safe with a single instance). Turn off to migrate
    /// by hand instead.
    /// </summary>
    public bool MigrateOnStartup { get; set; } = true;
}
