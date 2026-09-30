using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Library;

/// <summary>A faction in the library, as the list shows it.</summary>
/// <param name="Id">The faction's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nation">Whose flag it shows (None: a plain one).</param>
/// <param name="UnitCount">How many units it has.</param>
public sealed record FactionSummary(Guid Id, string Name, Nation Nation, int UnitCount);

/// <summary>A faction in the library, with its units.</summary>
/// <param name="Id">The faction's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="Nation">Whose flag it shows (None: a plain one).</param>
/// <param name="Units">Its units, sorted by name.</param>
public sealed record FactionResponse(
    Guid Id,
    string Name,
    Nation Nation,
    IReadOnlyList<UnitResponse> Units
);

/// <summary>Adds a faction to the library, or changes one.</summary>
/// <param name="Name">Its name (duplicates are allowed).</param>
/// <param name="Nation">Whose flag it shows (None: a plain one).</param>
public sealed record SaveFactionRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: JsonRequired, EnumDataType(typeof(Nation))] Nation Nation
);

/// <summary>A unit in the library.</summary>
/// <param name="Id">The unit's ID.</param>
/// <param name="FactionId">Its faction.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor (FF).</param>
/// <param name="Points">What it's worth.</param>
public sealed record UnitResponse(
    Guid Id,
    Guid FactionId,
    string Name,
    UnitType Type,
    int FightingFactor,
    int Points
);

/// <summary>Adds a unit to a faction in the library, or changes one.</summary>
/// <param name="Name">Its name (duplicates are allowed).</param>
/// <param name="Type">What kind of troops it is.</param>
/// <param name="FightingFactor">Its Fighting Factor (FF).</param>
/// <param name="Points">What it's worth.</param>
public sealed record SaveUnitRequest(
    [property: Trimmed, Required, StringLength(100)] string Name,
    [property: JsonRequired, EnumDataType(typeof(UnitType))] UnitType Type,
    [property: JsonRequired, Range(UnitStats.MinFightingFactor, UnitStats.MaxFightingFactor)]
        int FightingFactor,
    [property: JsonRequired, Range(UnitStats.MinPoints, UnitStats.MaxPoints)] int Points
);
