namespace Wwg.Api.Features.Admin;

/// <summary>A user in the admin user list.</summary>
/// <param name="Id">The user's ID.</param>
/// <param name="Email">Their email (what they sign in with).</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="IsAdmin">Whether they're a site-wide Admin.</param>
/// <param name="CreatedAt">When they registered (UTC).</param>
public sealed record UserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsAdmin,
    DateTime CreatedAt
);

/// <summary>
/// One user, for admins. Their campaigns and roles are added with campaigns (Phase 3).
/// </summary>
/// <param name="Id">The user's ID.</param>
/// <param name="Email">Their email (what they sign in with).</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="IsAdmin">Whether they're a site-wide Admin.</param>
/// <param name="CreatedAt">When they registered (UTC).</param>
/// <param name="LockedOutUntil">When a sign-in lockout ends (UTC), if they're locked out now.</param>
public sealed record UserDetails(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsAdmin,
    DateTime CreatedAt,
    DateTime? LockedOutUntil
);
