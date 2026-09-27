using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure.Auth;

/// <summary>The site-wide Admins (<c>Admin</c> section).</summary>
internal sealed class AdminOptions
{
    public const string SectionName = "Admin";

    /// <summary>
    /// The full list of Admin emails, applied at startup only. Register the account first, then
    /// add its email here and restart (DESIGN.md §3.5).
    /// </summary>
    public IReadOnlyList<string> Emails { get; set; } = [];
}

internal static partial class AdminSync
{
    /// <summary>
    /// Makes the Admin role match <see cref="AdminOptions.Emails"/>: listed accounts get it,
    /// unlisted Admins lose it. Never at sign-up or on email change, only here: emails aren't
    /// verified, so whoever registered a listed address first would otherwise become Admin.
    /// </summary>
    public static async Task SyncAdminsAsync(this WebApplication app)
    {
        try
        {
            await SyncAsync(app);
        }
        catch (SqliteException exception)
        {
            // E.g. migrations turned off and not yet applied. Start anyway: /health reports the
            // database problem, rather than the container crash-looping.
            LogFailed(app.Services.GetRequiredService<ILogger<AdminOptions>>(), exception);
        }
    }

    private static async Task SyncAsync(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var emails = services.GetRequiredService<IOptions<AdminOptions>>().Value.Emails;
        var users = services.GetRequiredService<UserManager<AppUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var logger = services.GetRequiredService<ILogger<AdminOptions>>();

        if (!await roles.RoleExistsAsync(Roles.Admin))
        {
            await roles.CreateAsync(
                new IdentityRole<Guid>(Roles.Admin) { Id = Guid.CreateVersion7() }
            );
        }

        var listed = emails
            .Select(email => users.NormalizeEmail(email.Trim()))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var admin in await users.GetUsersInRoleAsync(Roles.Admin))
        {
            if (admin.NormalizedEmail is null || !listed.Contains(admin.NormalizedEmail))
            {
                await users.RemoveFromRoleAsync(admin, Roles.Admin);
                // Their current tokens still say Admin; this makes them stop working now.
                await users.UpdateSecurityStampAsync(admin);
                LogRemoved(logger, admin.Email);
            }
        }

        foreach (var email in listed)
        {
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                LogNotRegistered(logger, email);
            }
            else if (!await users.IsInRoleAsync(user, Roles.Admin))
            {
                // No stamp change: their next refresh (within 30 minutes) picks up the role.
                await users.AddToRoleAsync(user, Roles.Admin);
                LogAdded(logger, user.Email);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't sync the Admin role at startup")]
    private static partial void LogFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Admin role granted to {Email}")]
    private static partial void LogAdded(ILogger logger, string? email);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Admin role removed from {Email}")]
    private static partial void LogRemoved(ILogger logger, string? email);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Admin:Emails lists {Email}, which has no account yet; it gets the role on the next restart after registering"
    )]
    private static partial void LogNotRegistered(ILogger logger, string email);
}
