namespace Wwg.Api.Data.Entities;

/// <summary>How much of a sighted force's strength its observers learn (decision 0020).</summary>
public enum SightingStrength
{
    Hidden,

    /// <summary>A rough size, as the Umpire judged it.</summary>
    Rough,

    /// <summary>Its points.</summary>
    Exact,
}

/// <summary>A sighted force's rough size, as the Umpire judges it.</summary>
public enum ForceSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// What an army saw of a hex of the other side's units, for a turn (decision 0020), as the Umpire
/// shaped it when that turn started: kept, drawn in full on its turn and faded for the 3 after.
/// What was seen is copied in, as it was then.
/// </summary>
internal sealed class Sighting : Entity
{
    public Guid ObservingArmyId { get; set; }

    public Army ObservingArmy { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The turn it was made for, and seen on.</summary>
    public int Turn { get; set; }

    /// <summary>The hex; told to its observers only if <see cref="ShowsHex"/>.</summary>
    public int Q { get; set; }

    public int R { get; set; }

    public bool ShowsHex { get; set; }

    /// <summary>Where it was, roughly, from the observing army's nearest unit.</summary>
    public required string Whereabouts { get; set; }

    /// <summary>The armies seen there, if shown.</summary>
    public List<Guid>? ArmyIds { get; set; }

    /// <summary>Each unit's type, if shown.</summary>
    public List<UnitType>? UnitTypes { get; set; }

    public SightingStrength Strength { get; set; }

    public ForceSize? Size { get; set; }

    /// <summary>Their points, if the strength shown is exact.</summary>
    public int? Points { get; set; }

    /// <summary>Added by the Umpire (spies, scouting parties) rather than found by sight.</summary>
    public bool ByUmpire { get; set; }

    /// <summary>The ally whose report brought it (step 49c), or null for the army's own.</summary>
    public Guid? SharedByArmyId { get; set; }
}
