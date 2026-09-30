namespace Wwg.Api.Data.Entities;

/// <summary>
/// What kind of troops a unit is (stored as its name), grouped by the rule book's movement
/// classes (decision 0014): infantry and foot artillery; light infantry and partisans; light
/// cavalry and scouts; medium and heavy cavalry and horse artillery; supply and siege artillery;
/// and boats, which keep to waterways and lakes (decision 0016).
/// </summary>
public enum UnitType
{
    LineInfantry,
    FootArtillery,
    Engineers,
    LightInfantry,
    Partisans,
    LightCavalry,
    Scouts,
    MediumCavalry,
    HeavyCavalry,
    HorseArtillery,
    SupplyTrain,
    SiegeArtillery,
    Boat,
}

/// <summary>The bounds of a unit's numbers, in the library and in a campaign.</summary>
internal static class UnitStats
{
    /// <summary>The lowest and highest Fighting Factor.</summary>
    public const int MinFightingFactor = 1,
        MaxFightingFactor = 9;

    /// <summary>The fewest and most points a unit can be worth.</summary>
    public const int MinPoints = 0,
        MaxPoints = 100;
}

/// <summary>
/// A unit in an army: a copy of a library unit, taken when it joined the campaign (decision 0015),
/// so what happens in the campaign changes only this. Every member sees it; where it is follows
/// the visibility rule.
/// </summary>
internal sealed class ArmyUnit : Entity
{
    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The army's campaign, kept here so a library unit is in it at most once.</summary>
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The library unit it was copied from.</summary>
    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }

    public UnitType Type { get; set; }

    /// <summary>The unit's Fighting Factor ("FF"), <see cref="UnitStats.MinFightingFactor"/>–<see cref="UnitStats.MaxFightingFactor"/>.</summary>
    public int FightingFactor { get; set; }

    /// <summary>What the unit is worth, <see cref="UnitStats.MinPoints"/>–<see cref="UnitStats.MaxPoints"/>.</summary>
    public int Points { get; set; }
}
