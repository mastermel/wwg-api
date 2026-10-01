namespace Wwg.Api.Data.Entities;

/// <summary>A depot's part in supply (decision 0019).</summary>
public enum DepotKind
{
    /// <summary>Where supply comes from: it can't itself be out of supply.</summary>
    Main,

    /// <summary>
    /// Supplied by its own route from a main depot of its army; stocked to go on supplying for 15
    /// turns once cut off.
    /// </summary>
    Intermediate,
}

/// <summary>
/// One of an army's depots (decision 0019): only its own army's units draw supply from it. The
/// Umpire places, moves, captures and destroys them. Its army's commander and the Umpire see it.
/// </summary>
internal sealed class Depot : Entity
{
    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    public DepotKind Kind { get; set; }

    /// <summary>Its name, if given ("Charleroi").</summary>
    public string? Name { get; set; }

    /// <summary>The hex it's in.</summary>
    public int Q { get; set; }

    public int R { get; set; }

    /// <summary>
    /// An intermediate depot's turns in a row cut off from its army's main depots, counted as each
    /// turn closes (step 48c); it stops supplying after 15.
    /// </summary>
    public int CutOffTurns { get; set; }
}
