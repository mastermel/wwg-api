namespace Wwg.Api.Data.Entities;

/// <summary>What kind of troops a unit is (stored as its name).</summary>
public enum UnitType
{
    HeavyInfantry,
    LightInfantry,
    Skirmishers,
    HeavyCavalry,
    LightCavalry,
    FootArtillery,
    HorseArtillery,
}

/// <summary>A unit in an army. Every member sees it; where it is follows the visibility rule.</summary>
internal sealed class Unit : Entity
{
    /// <summary>The lowest and highest Fighting Factor.</summary>
    public const int MinFightingFactor = 1,
        MaxFightingFactor = 9;

    /// <summary>The fewest and most points a unit can be worth.</summary>
    public const int MinPoints = 0,
        MaxPoints = 100;

    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }

    public UnitType Type { get; set; }

    /// <summary>The unit's Fighting Factor ("FF"), <see cref="MinFightingFactor"/>–<see cref="MaxFightingFactor"/>.</summary>
    public int FightingFactor { get; set; }

    /// <summary>What the unit is worth, <see cref="MinPoints"/>–<see cref="MaxPoints"/>.</summary>
    public int Points { get; set; }
}
