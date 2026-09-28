using System.ComponentModel.DataAnnotations;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Campaigns;

/// <summary>Starts a campaign; the caller becomes its Umpire.</summary>
public sealed record CreateCampaignRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: Trimmed, StringLength(2000)] string? Description
);

/// <summary>Changes a campaign's name and description.</summary>
public sealed record UpdateCampaignRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: Trimmed, StringLength(2000)] string? Description
);

/// <summary>A campaign in the "my campaigns" list.</summary>
/// <param name="Id">The campaign's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="MyRole">The caller's role in it.</param>
/// <param name="UmpireName">The Umpire's name, or null if it has none.</param>
/// <param name="PlayerCount">How many Players it has.</param>
public sealed record CampaignSummary(
    Guid Id,
    string Name,
    CampaignRole MyRole,
    string? UmpireName,
    int PlayerCount
);

/// <summary>The campaign's Umpire.</summary>
/// <param name="MemberId">Their membership's ID.</param>
/// <param name="UserId">Their user ID.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
public sealed record CampaignUmpire(Guid MemberId, Guid UserId, string FirstName, string LastName);

/// <summary>A campaign's details.</summary>
/// <param name="Id">The campaign's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Description">Its description, if any.</param>
/// <param name="Umpire">The Umpire, or null if the campaign has none (their account was deleted).</param>
/// <param name="MyRole">The caller's role, or null for an Admin who isn't a member.</param>
/// <param name="PlayerCount">How many Players it has.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
public sealed record CampaignResponse(
    Guid Id,
    string Name,
    string? Description,
    CampaignUmpire? Umpire,
    CampaignRole? MyRole,
    int PlayerCount,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

/// <summary>A campaign's join code. The app builds the join link from it.</summary>
/// <param name="JoinCode">The code, safe to use in a URL as it is.</param>
public sealed record JoinCodeResponse(string JoinCode);

/// <summary>A member of a campaign.</summary>
/// <param name="Id">The membership's ID (used to remove a Player).</param>
/// <param name="UserId">Their user ID.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="Role">Their role in the campaign.</param>
/// <param name="JoinedAt">When they joined (UTC).</param>
/// <param name="Army">The army they command, or null.</param>
public sealed record CampaignMemberResponse(
    Guid Id,
    Guid UserId,
    string FirstName,
    string LastName,
    CampaignRole Role,
    DateTime JoinedAt,
    MemberArmy? Army
);

/// <summary>The army a member commands.</summary>
/// <param name="Id">The army's ID.</param>
/// <param name="Name">Its name.</param>
public sealed record MemberArmy(Guid Id, string Name);
