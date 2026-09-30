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

/// <summary>
/// The town or city in a hex, if any (decision 0016): the map legend's small and large cities.
/// </summary>
public enum SettlementSize
{
    None,
    Town,
    City,
}

/// <summary>Whether a town or city is a capital (it scores more).</summary>
public enum CapitalStatus
{
    None,
    Minor,
    Capital,
}

/// <summary>
/// A navigable course across an edge (decision 0016), for boats: which way it flows, out of the
/// hex the edge is stored on and into the one across, or back in.
/// </summary>
public enum Waterway
{
    None,
    Out,
    In,
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

    public SettlementSize SettlementSize { get; set; }

    /// <summary>The town or city is walled.</summary>
    public bool Walled { get; set; }

    /// <summary>A fortress, in a town or city or on its own.</summary>
    public bool Fortress { get; set; }

    public CapitalStatus Capital { get; set; }

    /// <summary>The town, city or fortress's name, if given.</summary>
    public string? Name { get; set; }

    /// <summary>Set by the Umpire: inference leaves it alone.</summary>
    public bool SetByUmpire { get; set; }
}

/// <summary>
/// An edge of a campaign's grid with a road, river or waterway on it, on hex (Q, R)'s
/// <see cref="Side"/>; an edge without one has none of them. The river lies along the edge,
/// between the hexes; a waterway runs across it, from one to the other. Inferred, or set by the Umpire (and then kept).
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

    /// <summary>A navigable river's course across the edge, from hex to hex (boats follow it).</summary>
    public Waterway Waterway { get; set; }

    /// <summary>Set by the Umpire: inference leaves it alone.</summary>
    public bool SetByUmpire { get; set; }
}
