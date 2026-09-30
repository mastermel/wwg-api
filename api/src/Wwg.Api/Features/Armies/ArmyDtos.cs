using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Armies;

/// <summary>Adds an army to the campaign (at most 8).</summary>
/// <param name="Name">The army's name.</param>
/// <param name="CommanderMemberId">A Player to command it (their membership ID), or null.</param>
/// <param name="SideId">Its side, or null for none yet ("Unassigned").</param>
/// <param name="Color">Its colour, or null for the first one no other army has.</param>
/// <param name="Nation">The nation it fights for (its flag), or null for none (a plain flag).</param>
public sealed record CreateArmyRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    Guid? CommanderMemberId,
    Guid? SideId = null,
    [property: EnumDataType(typeof(ArmyColor))] ArmyColor? Color = null,
    [property: EnumDataType(typeof(Nation))] Nation? Nation = null
);

// Colour and nation are [JsonRequired]: left out, they'd quietly read as Red and None.

/// <summary>Changes an army's name, side, colour and nation.</summary>
/// <param name="Name">The army's name.</param>
/// <param name="SideId">Its side, or null for none ("Unassigned").</param>
/// <param name="Color">Its colour. Two armies can share one.</param>
/// <param name="Nation">The nation it fights for, drawn as its flag.</param>
public sealed record UpdateArmyRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    Guid? SideId,
    [property: JsonRequired, EnumDataType(typeof(ArmyColor))] ArmyColor Color,
    [property: JsonRequired, EnumDataType(typeof(Nation))] Nation Nation
);

/// <summary>The side an army is in.</summary>
/// <param name="Id">The side's ID.</param>
/// <param name="Name">Its name.</param>
public sealed record ArmySide(Guid Id, string Name);

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
/// <param name="Side">Its side, or null if it has none yet.</param>
/// <param name="Color">Its colour.</param>
/// <param name="Nation">The nation it fights for, drawn as its flag.</param>
public sealed record ArmySummary(
    Guid Id,
    string Name,
    ArmyCommander? Commander,
    ArmySide? Side,
    ArmyColor Color,
    Nation Nation
);

/// <summary>An army's details, with its units.</summary>
/// <param name="Id">The army's ID.</param>
/// <param name="CampaignId">The campaign it's in.</param>
/// <param name="CampaignName">That campaign's name.</param>
/// <param name="Name">Its name.</param>
/// <param name="Commander">Its commander, or null if unassigned.</param>
/// <param name="Side">Its side, or null if it has none yet.</param>
/// <param name="Color">Its colour.</param>
/// <param name="Nation">The nation it fights for, drawn as its flag.</param>
/// <param name="Units">Its units, sorted by name.</param>
/// <param name="CreatedAt">When it was created (UTC).</param>
/// <param name="UpdatedAt">When it last changed (UTC).</param>
public sealed record ArmyResponse(
    Guid Id,
    Guid CampaignId,
    string CampaignName,
    string Name,
    ArmyCommander? Commander,
    ArmySide? Side,
    ArmyColor Color,
    Nation Nation,
    IReadOnlyList<ArmyUnitResponse> Units,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
