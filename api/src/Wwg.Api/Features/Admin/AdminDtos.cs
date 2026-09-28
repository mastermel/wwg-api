using Wwg.Api.Data.Entities;

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

/// <summary>One user, for admins, with the campaigns they're in.</summary>
/// <param name="Id">The user's ID.</param>
/// <param name="Email">Their email (what they sign in with).</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="IsAdmin">Whether they're a site-wide Admin.</param>
/// <param name="CreatedAt">When they registered (UTC).</param>
/// <param name="LockedOutUntil">When a sign-in lockout ends (UTC), if they're locked out now.</param>
/// <param name="Campaigns">The campaigns they're in, with their role, sorted by name.</param>
public sealed record UserDetails(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsAdmin,
    DateTime CreatedAt,
    DateTime? LockedOutUntil,
    IReadOnlyList<UserCampaign> Campaigns
);

/// <summary>A campaign a user is in.</summary>
/// <param name="Id">The campaign's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Role">The user's role in it.</param>
public sealed record UserCampaign(Guid Id, string Name, CampaignRole Role);

/// <summary>A campaign in the admin list of every campaign.</summary>
/// <param name="Id">The campaign's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="UmpireName">The Umpire's name, or null if it has none.</param>
/// <param name="PlayerCount">How many Players it has.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
public sealed record AdminCampaignSummary(
    Guid Id,
    string Name,
    string? UmpireName,
    int PlayerCount,
    DateTime CreatedAt
);

/// <summary>Makes a user the campaign's Umpire.</summary>
/// <param name="UserId">The new Umpire: a Player in the campaign, or any other user.</param>
public sealed record SetUmpireRequest(Guid UserId);
