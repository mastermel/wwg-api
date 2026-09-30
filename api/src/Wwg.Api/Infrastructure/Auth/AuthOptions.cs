using System.ComponentModel.DataAnnotations;

namespace Wwg.Api.Infrastructure.Auth;

/// <summary>Sign-in settings (<c>Auth</c> section).</summary>
internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Where the Data Protection keys that protect tokens are kept. They must survive restarts, or
    /// every restart signs everyone out. A relative path is resolved against the content root.
    /// </summary>
    [Required]
    public string? DataProtectionKeysPath { get; set; }

    /// <summary>How long an access token works (kept in memory by the SPA).</summary>
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>How long a refresh token works. Sliding: each refresh issues a new one.</summary>
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(30);

    /// <summary>
    /// How long an Admin's masquerade as another user lasts before it ends on its own (decision
    /// 0012): neither of its tokens outlives it.
    /// </summary>
    public TimeSpan MasqueradeLifetime { get; set; } = TimeSpan.FromHours(8);

    /// <summary>
    /// How long a password reset link works (Identity's default is a day). Identity checks it
    /// against the system clock, not the injected TimeProvider.
    /// </summary>
    public TimeSpan PasswordResetLinkLifetime { get; set; } = TimeSpan.FromHours(2);
}
