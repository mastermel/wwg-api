using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.ArmyUnits;

/// <summary>Adds library units to an army, from the factions it takes units from.</summary>
/// <param name="UnitIds">The library units: none already in the campaign.</param>
public sealed record AddArmyUnitsRequest(
    [property: Required, MinLength(1), MaxLength(100)] IReadOnlyList<Guid> UnitIds
);

// The numbers and type are [JsonRequired]: left out, they'd quietly read as 0 or Line Infantry.
// EnumDataType refuses a type sent as an undefined number (the enum converter accepts numbers).

/// <summary>Changes the campaign's copy of a unit (the library's stays as it is).</summary>
/// <param name="Name">The unit's name.</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor ("FF"), 1–9.</param>
/// <param name="Points">What it's worth, 0–100.</param>
public sealed record UpdateArmyUnitRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: JsonRequired, EnumDataType(typeof(UnitType))] UnitType Type,
    [property: JsonRequired, Range(UnitStats.MinFightingFactor, UnitStats.MaxFightingFactor)]
        int FightingFactor,
    [property: JsonRequired, Range(UnitStats.MinPoints, UnitStats.MaxPoints)] int Points
);

/// <summary>A unit in an army: the campaign's copy of a library unit.</summary>
/// <param name="Id">The unit's ID.</param>
/// <param name="ArmyId">The army it's in.</param>
/// <param name="UnitId">The library unit it was copied from.</param>
/// <param name="FactionId">That library unit's faction.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor ("FF"), 1–9.</param>
/// <param name="Points">What it's worth, 0–100.</param>
public sealed record ArmyUnitResponse(
    Guid Id,
    Guid ArmyId,
    Guid UnitId,
    Guid FactionId,
    string Name,
    UnitType Type,
    int FightingFactor,
    int Points
);
