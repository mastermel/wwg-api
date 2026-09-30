using System.Globalization;
using System.Security.Claims;

namespace Wwg.Api.Infrastructure.Auth;

/// <summary>
/// An Admin using the app as another user (decision 0012). It lives in the session's tokens, as
/// claims beside the user's own: who started it, their security stamp then, and when it ends.
/// </summary>
internal sealed record Masquerade(
    Guid AdminId,
    string AdminName,
    string AdminStamp,
    DateTimeOffset Ends
)
{
    private const string AdminIdClaim = "wwg:masquerade:admin";
    private const string AdminNameClaim = "wwg:masquerade:admin-name";
    private const string AdminStampClaim = "wwg:masquerade:admin-stamp";
    private const string EndsClaim = "wwg:masquerade:ends";

    /// <summary>The session's masquerade, or null if it isn't one.</summary>
    public static Masquerade? From(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(AdminIdClaim), out var adminId)
        && principal.FindFirstValue(AdminNameClaim) is { } name
        && principal.FindFirstValue(AdminStampClaim) is { } stamp
        && long.TryParse(
            principal.FindFirstValue(EndsClaim),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var ends
        )
            ? new Masquerade(adminId, name, stamp, DateTimeOffset.FromUnixTimeSeconds(ends))
            : null;

    public IEnumerable<Claim> ToClaims() =>
        [
            new(AdminIdClaim, AdminId.ToString()),
            new(AdminNameClaim, AdminName),
            new(AdminStampClaim, AdminStamp),
            new(EndsClaim, Ends.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)),
        ];
}

/// <summary>The app log's record of masquerades (decision 0012): who, as whom, when.</summary>
internal static partial class MasqueradeLog
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Admin {AdminId} began masquerading as user {UserId}, until {Ends}."
    )]
    public static partial void Started(
        ILogger logger,
        Guid adminId,
        Guid userId,
        DateTimeOffset ends
    );

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Admin {AdminId} ended masquerading as user {UserId}."
    )]
    public static partial void Ended(ILogger logger, Guid adminId, Guid userId);
}
