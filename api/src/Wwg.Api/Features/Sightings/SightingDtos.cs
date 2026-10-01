using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Sightings;

/// <summary>A unit in a hex another army can see (decision 0020), for the Umpire.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="ArmyId">Its army.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Points">Its points.</param>
public sealed record SightedUnitResponse(
    Guid UnitId,
    Guid ArmyId,
    string Name,
    UnitType Type,
    int Points
);

/// <summary>
/// A sighting the closing turn gives (decision 0020): another side's hex an army can see, for the
/// Umpire to shape when starting the next turn.
/// </summary>
/// <param name="ObservingArmyId">The army that sees it.</param>
/// <param name="Q">The hex (axial q).</param>
/// <param name="R">The hex (axial r).</param>
/// <param name="Whereabouts">Where it is, roughly, from the army's nearest unit.</param>
/// <param name="Screened">The observed side's light troops stand in the way: perhaps a screen.</param>
/// <param name="Units">The other side's units there.</param>
public sealed record SightingDueResponse(
    Guid ObservingArmyId,
    int Q,
    int R,
    string Whereabouts,
    bool Screened,
    IReadOnlyList<SightedUnitResponse> Units
);

/// <summary>The Umpire's shaping of one sighting, when starting the next turn (decision 0020).</summary>
/// <param name="ObservingArmyId">The army that sees it.</param>
/// <param name="Q">The hex of the other side's units (axial q).</param>
/// <param name="R">The hex (axial r).</param>
/// <param name="ShowsHex">Whether its observers learn the hex, or only roughly where.</param>
/// <param name="ShowsArmies">Whether they learn whose troops (army and nation).</param>
/// <param name="ShowsTypes">Whether they learn each unit's type.</param>
/// <param name="Strength">Hidden, a rough size, or their points.</param>
/// <param name="Size">The rough size, when that's what's shown.</param>
public sealed record SightingRequest(
    [property: JsonRequired] Guid ObservingArmyId,
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R,
    [property: JsonRequired] bool ShowsHex,
    [property: JsonRequired] bool ShowsArmies,
    [property: JsonRequired] bool ShowsTypes,
    [property: JsonRequired, EnumDataType(typeof(SightingStrength))] SightingStrength Strength,
    [property: EnumDataType(typeof(ForceSize))] ForceSize? Size = null
);

/// <summary>What an army saw of a hex of the other side's units, on a turn (decision 0020).</summary>
/// <param name="Id">The sighting.</param>
/// <param name="ObservingArmyId">The army that saw it.</param>
/// <param name="Turn">The turn it was made for, and seen on.</param>
/// <param name="Q">The hex, if shown (axial q).</param>
/// <param name="R">The hex, if shown (axial r).</param>
/// <param name="Latitude">The hex's centre, if shown.</param>
/// <param name="Longitude">The hex's centre, if shown.</param>
/// <param name="Whereabouts">Where it was, roughly, from the army's nearest unit.</param>
/// <param name="ArmyIds">The armies seen, if shown.</param>
/// <param name="UnitTypes">Each unit's type, if shown.</param>
/// <param name="Strength">Hidden, rough or exact.</param>
/// <param name="Size">The rough size, if shown.</param>
/// <param name="Points">The points, if shown exactly.</param>
/// <param name="ByUmpire">Added by the Umpire (spies, scouting parties).</param>
/// <param name="SharedByArmyId">The ally whose report brought it, if one did.</param>
public sealed record SightingResponse(
    Guid Id,
    Guid ObservingArmyId,
    int Turn,
    int? Q,
    int? R,
    double? Latitude,
    double? Longitude,
    string Whereabouts,
    IReadOnlyList<Guid>? ArmyIds,
    IReadOnlyList<UnitType>? UnitTypes,
    SightingStrength Strength,
    ForceSize? Size,
    int? Points,
    bool ByUmpire,
    Guid? SharedByArmyId
);
