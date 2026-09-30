namespace Wwg.Api.Data.Entities;

/// <summary>
/// A faction in the club's library (decision 0015): a collection of units, such as the French or
/// the British, shared by every campaign. Not a campaign's side (<see cref="Side"/>).
/// </summary>
internal sealed class Faction : Entity
{
    public required string Name { get; set; }

    /// <summary>Whose flag it shows; None for a plain one.</summary>
    public Nation Nation { get; set; }
}

/// <summary>
/// A unit in the club's library (decision 0015), in one faction. A campaign takes a copy of it
/// (<see cref="ArmyUnit"/>); editing it changes only the campaigns it joins afterwards.
/// </summary>
internal sealed class Unit : Entity
{
    public Guid FactionId { get; set; }

    public Faction Faction { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }

    public UnitType Type { get; set; }

    /// <summary>Its Fighting Factor ("FF"), within <see cref="UnitStats"/>.</summary>
    public int FightingFactor { get; set; }

    /// <summary>What it's worth, within <see cref="UnitStats"/>.</summary>
    public int Points { get; set; }
}
