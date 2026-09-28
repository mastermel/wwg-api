using System.ComponentModel.DataAnnotations;
using Wwg.Api.Features.Units;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Armies;

/// <summary>Adds an army to the campaign.</summary>
/// <param name="Name">The army's name.</param>
/// <param name="CommanderMemberId">A Player to command it (their membership ID), or null.</param>
public sealed record CreateArmyRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    Guid? CommanderMemberId
);

/// <summary>Renames an army.</summary>
/// <param name="Name">The army's new name.</param>
public sealed record RenameArmyRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>Gives an army a commander.</summary>
/// <param name="MemberId">The Player's membership ID.</param>
public sealed record AssignCommanderRequest(Guid MemberId);

/// <summary>The Player commanding an army.</summary>
/// <param name="MemberId">Their membership ID.</param>
/// <param name="UserId">Their user ID.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
public sealed record ArmyCommander(Guid MemberId, Guid UserId, string FirstName, string LastName);

/// <summary>An army in the campaign's army list. Every member sees every army.</summary>
/// <param name="Id">The army's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Commander">Its commander, or null if unassigned.</param>
public sealed record ArmySummary(Guid Id, string Name, ArmyCommander? Commander);

/// <summary>An army's details, with its units.</summary>
/// <param name="Id">The army's ID.</param>
/// <param name="CampaignId">The campaign it's in.</param>
/// <param name="CampaignName">That campaign's name.</param>
/// <param name="Name">Its name.</param>
/// <param name="Commander">Its commander, or null if unassigned.</param>
/// <param name="Units">Its units, sorted by name.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
public sealed record ArmyResponse(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    string Name,
    ArmyCommander? Commander,
    IReadOnlyList<UnitResponse> Units,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
