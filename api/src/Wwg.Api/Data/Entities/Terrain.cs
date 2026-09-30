namespace Wwg.Api.Data.Entities;

/// <summary>A hex's ground (decision 0014): what it costs to move into it.</summary>
public enum Terrain
{
    Flat,
    LowHill,
    HighHill,
    Mountain,
    Water,
}

/// <summary>The largest place in a hex, if any.</summary>
public enum Settlement
{
    None,
    SmallCity,
    LargeCity,
    WalledCity,
    Fortress,
}

/// <summary>
/// A hex side an edge is stored on: N, NE or SE. The other three (S, SW, NW) are the neighbours'
/// N, NE and SE, so every edge has one place.
/// </summary>
public enum EdgeSide
{
    N,
    NE,
    SE,
}

/// <summary>The best road across an edge.</summary>
public enum RoadQuality
{
    None,
    Poor,
    Good,
}

/// <summary>
/// A hex of a campaign's grid with something on it (decision 0014); a hex without one is Flat
/// with nothing on it. Inferred from the map's data, or set by the Umpire (and then kept when
/// inference runs again).
/// </summary>
internal sealed class HexCell : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public int Q { get; set; }

    public int R { get; set; }

    public Terrain Terrain { get; set; }

    public bool Forest { get; set; }

    public Settlement Settlement { get; set; }

    /// <summary>Set by the Umpire: inference leaves it alone.</summary>
    public bool SetByUmpire { get; set; }
}

/// <summary>
/// An edge of a campaign's grid with a road or river on it, on hex (Q, R)'s <see cref="Side"/>;
/// an edge without one has neither. Inferred, or set by the Umpire (and then kept).
/// </summary>
internal sealed class HexEdge : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public int Q { get; set; }

    public int R { get; set; }

    public EdgeSide Side { get; set; }

    public RoadQuality Road { get; set; }

    public bool River { get; set; }

    /// <summary>A bridge carries the road over the river: only on an edge with a river.</summary>
    public bool Bridge { get; set; }

    /// <summary>Set by the Umpire: inference leaves it alone.</summary>
    public bool SetByUmpire { get; set; }
}
