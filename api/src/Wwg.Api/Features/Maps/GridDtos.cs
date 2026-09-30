using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

// Enums and flags are [JsonRequired]: left out, they'd quietly read as Flat, None or false.
// EnumDataType refuses an enum sent as an undefined number.

/// <summary>A hex with terrain on it.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Terrain">Its ground.</param>
/// <param name="Forest">Whether it's mostly forest.</param>
/// <param name="Settlement">The largest place in it.</param>
/// <param name="SetByUmpire">Set by the Umpire, so inference leaves it alone.</param>
public sealed record HexCellResponse(
    int Q,
    int R,
    Terrain Terrain,
    bool Forest,
    Settlement Settlement,
    bool SetByUmpire
);

/// <summary>An edge with a road or river on it.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Side">The hex's side the edge is on: N, NE or SE (the others are its neighbours').</param>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river runs along it.</param>
/// <param name="Bridge">Whether a bridge crosses the river.</param>
/// <param name="SetByUmpire">Set by the Umpire, so inference leaves it alone.</param>
public sealed record HexEdgeResponse(
    int Q,
    int R,
    EdgeSide Side,
    RoadQuality Road,
    bool River,
    bool Bridge,
    bool SetByUmpire
);

/// <summary>
/// A campaign's terrain: the hexes and edges with something on them. Any other hex is Flat with
/// nothing on it, and any other edge has no road or river.
/// </summary>
/// <param name="Cells">The hexes with terrain.</param>
/// <param name="Edges">The edges with roads or rivers.</param>
public sealed record CampaignGridResponse(
    IReadOnlyList<HexCellResponse> Cells,
    IReadOnlyList<HexEdgeResponse> Edges
);

/// <summary>A hex's inferred terrain.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Terrain">Its ground.</param>
/// <param name="Forest">Whether it's mostly forest.</param>
/// <param name="Settlement">The largest place in it.</param>
public sealed record InferredHexCell(
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R,
    [property: JsonRequired, EnumDataType(typeof(Terrain))] Terrain Terrain,
    [property: JsonRequired] bool Forest,
    [property: JsonRequired, EnumDataType(typeof(Settlement))] Settlement Settlement
);

/// <summary>An edge's inferred road and river.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Side">The hex's side: N, NE or SE.</param>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river runs along it.</param>
/// <param name="Bridge">Whether a bridge crosses the river (only with a river).</param>
public sealed record InferredHexEdge(
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R,
    [property: JsonRequired, EnumDataType(typeof(EdgeSide))] EdgeSide Side,
    [property: JsonRequired, EnumDataType(typeof(RoadQuality))] RoadQuality Road,
    [property: JsonRequired] bool River,
    [property: JsonRequired] bool Bridge
);

/// <summary>
/// The terrain inferred for the whole grid. It replaces what inference saved before; what the
/// Umpire set stays. Hexes and edges with nothing on them can be left out.
/// </summary>
/// <param name="Cells">The hexes, each once, inside the grid.</param>
/// <param name="Edges">The edges, each once, touching the grid.</param>
public sealed record SaveCampaignGridRequest(
    [property: Required, MaxLength(GridLimits.MaxCells)] IReadOnlyList<InferredHexCell> Cells,
    [property: Required, MaxLength(GridLimits.MaxEdges)] IReadOnlyList<InferredHexEdge> Edges
);

/// <summary>The Umpire sets a hex's terrain.</summary>
/// <param name="Terrain">Its ground.</param>
/// <param name="Forest">Whether it's mostly forest.</param>
/// <param name="Settlement">The largest place in it.</param>
public sealed record UpdateHexCellRequest(
    [property: JsonRequired, EnumDataType(typeof(Terrain))] Terrain Terrain,
    [property: JsonRequired] bool Forest,
    [property: JsonRequired, EnumDataType(typeof(Settlement))] Settlement Settlement
);

/// <summary>The Umpire sets an edge's road and river.</summary>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river runs along it.</param>
/// <param name="Bridge">Whether a bridge crosses the river (only with a river).</param>
public sealed record UpdateHexEdgeRequest(
    [property: JsonRequired, EnumDataType(typeof(RoadQuality))] RoadQuality Road,
    [property: JsonRequired] bool River,
    [property: JsonRequired] bool Bridge
);

/// <summary>How much terrain one save can carry.</summary>
internal static class GridLimits
{
    /// <summary>
    /// The most hexes with terrain: twice the most the map draws (hex-grid.ts' maxDrawnHexes).
    /// </summary>
    public const int MaxCells = 60_000;

    /// <summary>Three edges per hex.</summary>
    public const int MaxEdges = 3 * MaxCells;
}
