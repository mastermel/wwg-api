namespace Wwg.Api.Data.Entities;

/// <summary>The rule book's movement classes (section XII.E; decision 0014).</summary>
public enum MovementClass
{
    /// <summary>Line infantry, foot artillery, engineers.</summary>
    Infantry,

    /// <summary>Light infantry, partisans.</summary>
    Light,

    /// <summary>Light cavalry, scouts.</summary>
    LightCavalry,

    /// <summary>Medium and heavy cavalry, horse artillery.</summary>
    Cavalry,

    /// <summary>Supply trains, siege artillery.</summary>
    Slow,
}

/// <summary>The movement table's columns: the ground a step is on (the rules, §E.1).</summary>
public enum Ground
{
    GoodRoad,
    PoorRoad,
    Flat,
    LowHill,
    HighHill,
    Mountain,
}

/// <summary>
/// One cell of a campaign's movement table (step 44): how many hexes a turn a class moves on a
/// ground; 0, it can't. A campaign without rows moves by the rules' table; the Umpire's rows
/// replace it.
/// </summary>
internal sealed class MovementRate : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public MovementClass Class { get; set; }

    public Ground Ground { get; set; }

    /// <summary>Hexes a turn, in halves (0.5: a hex takes two turns); 0 for "can't".</summary>
    public double Hexes { get; set; }
}
