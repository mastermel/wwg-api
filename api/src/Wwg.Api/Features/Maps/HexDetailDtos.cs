using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

/// <summary>What's in a hex, by the rule book's table (p. 57).</summary>
/// <param name="Scrub">Scrub.</param>
/// <param name="Village">A small village.</param>
/// <param name="Woods">Small woods.</param>
/// <param name="Forest">Forest.</param>
/// <param name="Farms">Farms, or farmland.</param>
/// <param name="Fields">Fields.</param>
/// <param name="Streams">Streams.</param>
public sealed record HexFeatures(
    [property: JsonRequired] bool Scrub,
    [property: JsonRequired] bool Village,
    [property: JsonRequired] bool Woods,
    [property: JsonRequired] bool Forest,
    [property: JsonRequired] bool Farms,
    [property: JsonRequired] bool Fields,
    [property: JsonRequired] bool Streams
);

/// <summary>The dice as they fell.</summary>
/// <param name="Red">The red die, after its modifier (0–7).</param>
/// <param name="White">The white die.</param>
/// <param name="Green">The green die, if favourability was rolled.</param>
public sealed record HexDice(int Red, int White, int? Green);

/// <summary>A hex's actual terrain (decision 0016).</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Relief">The lie of the land.</param>
/// <param name="Features">What's there.</param>
/// <param name="Dominant">Its dominant feature.</param>
/// <param name="Favorability">How the ground favours the army that asked.</param>
/// <param name="Dice">The dice, or null if the Umpire set it.</param>
/// <param name="ForArmyId">The army that asked (the Umpire only sees it; null for others).</param>
/// <param name="ShownToArmyIds">The armies it's shown to (the Umpire only; empty for others).</param>
/// <param name="ShownToAll">Whether every member sees it.</param>
public sealed record HexDetailResponse(
    int Q,
    int R,
    DetailRelief Relief,
    HexFeatures Features,
    DominantFeature Dominant,
    Favorability Favorability,
    HexDice? Dice,
    Guid? ForArmyId,
    IReadOnlyList<Guid> ShownToArmyIds,
    bool ShownToAll
);

/// <summary>The Umpire shakes the dice for a hex (again: the new roll replaces the old).</summary>
/// <param name="ForArmyId">The army that asked, if any.</param>
/// <param name="Favorability">Roll the green die too: both sides have come onto the field together.</param>
/// <param name="FlatMinusOne">Take one off the red die (a flat hex only, at the Umpire's choice).</param>
public sealed record RollHexDetailRequest(
    Guid? ForArmyId,
    [property: JsonRequired] bool Favorability,
    [property: JsonRequired] bool FlatMinusOne
);

/// <summary>The Umpire sets a hex's actual terrain, and who sees it.</summary>
/// <param name="Relief">The lie of the land.</param>
/// <param name="Features">What's there.</param>
/// <param name="Dominant">Its dominant feature.</param>
/// <param name="Favorability">How the ground favours the army that asked.</param>
/// <param name="ForArmyId">The army that asked, if any.</param>
/// <param name="ShownToArmyIds">The armies it's shown to.</param>
/// <param name="ShownToAll">Show it to every member.</param>
public sealed record UpdateHexDetailRequest(
    [property: JsonRequired, EnumDataType(typeof(DetailRelief))] DetailRelief Relief,
    [property: Required] HexFeatures Features,
    [property: JsonRequired, EnumDataType(typeof(DominantFeature))] DominantFeature Dominant,
    [property: JsonRequired, EnumDataType(typeof(Favorability))] Favorability Favorability,
    Guid? ForArmyId,
    [property: Required, MaxLength(Army.MaxPerCampaign)] IReadOnlyList<Guid> ShownToArmyIds,
    [property: JsonRequired] bool ShownToAll
);
