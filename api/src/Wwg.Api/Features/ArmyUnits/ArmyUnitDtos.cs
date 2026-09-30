using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.ArmyUnits;

// The numbers and type are [JsonRequired]: left out, they'd quietly read as 0 or Heavy Infantry.
// EnumDataType refuses a type sent as an undefined number (the enum converter accepts numbers).

/// <summary>Adds a unit to an army.</summary>
/// <param name="Name">The unit's name.</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor ("FF"), 1–9.</param>
/// <param name="Points">What it's worth, 0–100.</param>
public sealed record CreateArmyUnitRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: JsonRequired, EnumDataType(typeof(UnitType))] UnitType Type,
    [property: JsonRequired, Range(UnitStats.MinFightingFactor, UnitStats.MaxFightingFactor)]
        int FightingFactor,
    [property: JsonRequired, Range(UnitStats.MinPoints, UnitStats.MaxPoints)] int Points
);

/// <summary>Changes a unit.</summary>
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

/// <summary>A unit in an army.</summary>
/// <param name="Id">The unit's ID.</param>
/// <param name="ArmyId">The army it's in.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor ("FF"), 1–9.</param>
/// <param name="Points">What it's worth, 0–100.</param>
public sealed record ArmyUnitResponse(
    Guid Id,
    Guid ArmyId,
    string Name,
    UnitType Type,
    int FightingFactor,
    int Points
);
