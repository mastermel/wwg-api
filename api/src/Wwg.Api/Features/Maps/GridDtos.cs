using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Maps;

// Enums and flags are [JsonRequired]: left out, they'd quietly read as Flat, None or false.
// EnumDataType refuses an enum sent as an undefined number.

/// <summary>A hex's town, city or fortress (decision 0016).</summary>
/// <param name="Size">A town or city, or none.</param>
/// <param name="Walled">The town or city is walled (only with one).</param>
/// <param name="Fortress">A fortress, in the town or city or on its own.</param>
/// <param name="Capital">Whether the town or city is a capital (only with one).</param>
/// <param name="Name">Its name, if given (only with a town, city or fortress).</param>
/// <param name="VictoryPoints">
/// What the Umpire made it worth (step 50, decision 0021); null for the rules' value.
/// </param>
public sealed record HexSettlement(
    [property: JsonRequired, EnumDataType(typeof(SettlementSize))] SettlementSize Size,
    [property: JsonRequired] bool Walled,
    [property: JsonRequired] bool Fortress,
    [property: JsonRequired, EnumDataType(typeof(CapitalStatus))] CapitalStatus Capital,
    [property: Trimmed, StringLength(100)] string? Name = null,
    [property: Range(0, 1000)] int? VictoryPoints = null
)
{
    /// <summary>Nothing there.</summary>
    public static readonly HexSettlement None = new(
        SettlementSize.None,
        false,
        false,
        CapitalStatus.None
    );

    /// <summary>Whether there's a town, city or fortress.</summary>
    [JsonIgnore]
    public bool IsAny => Size != SettlementSize.None || Fortress;

    /// <summary>What's wrong with it, or null: walls, capitals and names need a place.</summary>
    public string? Problem() =>
        Size == SettlementSize.None && (Walled || Capital != CapitalStatus.None)
            ? "Only a town or city can be walled or a capital."
        : !IsAny && !string.IsNullOrEmpty(Name) ? "Only a town, city or fortress has a name."
        : !IsAny && VictoryPoints is not null ? "Only a town, city or fortress is worth points."
        : null;

    /// <summary>
    /// What holding it is worth (the rules, §C.3(b); decision 0021): the Umpire's value, or the
    /// highest of a town 10, a city 25, walled 35, a fortress 50, and 25 more for a capital or 10
    /// for a minor capital. Nothing there, nothing.
    /// </summary>
    [JsonIgnore]
    public int Value =>
        VictoryPoints
        ?? (
            !IsAny
                ? 0
                : new[]
                {
                    Size == SettlementSize.Town ? 10 : 0,
                    Size == SettlementSize.City ? 25 : 0,
                    Walled ? 35 : 0,
                    Fortress ? 50 : 0,
                }.Max()
                    + Capital switch
                    {
                        CapitalStatus.Capital => 25,
                        CapitalStatus.Minor => 10,
                        _ => 0,
                    }
        );
}

/// <summary>A hex with terrain on it.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Terrain">Its ground.</param>
/// <param name="Forest">Whether it's mostly forest.</param>
/// <param name="Settlement">Its town, city or fortress.</param>
/// <param name="SetByUmpire">Set by the Umpire, so inference leaves it alone.</param>
public sealed record HexCellResponse(
    int Q,
    int R,
    Terrain Terrain,
    bool Forest,
    HexSettlement Settlement,
    bool SetByUmpire
);

/// <summary>An edge with a road, river or waterway on it.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Side">The hex's side the edge is on: N, NE or SE (the others are its neighbours').</param>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river lies along it, between the hexes.</param>
/// <param name="Bridge">Whether a bridge crosses the river.</param>
/// <param name="Waterway">A navigable course across it, and which way it flows.</param>
/// <param name="SetByUmpire">Set by the Umpire, so inference leaves it alone.</param>
public sealed record HexEdgeResponse(
    int Q,
    int R,
    EdgeSide Side,
    RoadQuality Road,
    bool River,
    bool Bridge,
    Waterway Waterway,
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
/// <param name="Settlement">Its town, city or fortress.</param>
public sealed record InferredHexCell(
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R,
    [property: JsonRequired, EnumDataType(typeof(Terrain))] Terrain Terrain,
    [property: JsonRequired] bool Forest,
    [property: Required] HexSettlement Settlement
);

/// <summary>An edge's inferred road, river and waterway.</summary>
/// <param name="Q">The hex's q (east).</param>
/// <param name="R">The hex's r (south-east).</param>
/// <param name="Side">The hex's side: N, NE or SE.</param>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river lies along it, between the hexes.</param>
/// <param name="Bridge">Whether a bridge crosses the river (only with a river).</param>
/// <param name="Waterway">A navigable course across it, and which way it flows.</param>
public sealed record InferredHexEdge(
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R,
    [property: JsonRequired, EnumDataType(typeof(EdgeSide))] EdgeSide Side,
    [property: JsonRequired, EnumDataType(typeof(RoadQuality))] RoadQuality Road,
    [property: JsonRequired] bool River,
    [property: JsonRequired] bool Bridge,
    [property: JsonRequired, EnumDataType(typeof(Waterway))] Waterway Waterway
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
/// <param name="Settlement">Its town, city or fortress.</param>
public sealed record UpdateHexCellRequest(
    [property: JsonRequired, EnumDataType(typeof(Terrain))] Terrain Terrain,
    [property: JsonRequired] bool Forest,
    [property: Required] HexSettlement Settlement
);

/// <summary>The Umpire sets an edge's road, river and waterway.</summary>
/// <param name="Road">The best road across it.</param>
/// <param name="River">Whether a river lies along it, between the hexes.</param>
/// <param name="Bridge">Whether a bridge crosses the river (only with a river).</param>
/// <param name="Waterway">A navigable course across it, and which way it flows.</param>
public sealed record UpdateHexEdgeRequest(
    [property: JsonRequired, EnumDataType(typeof(RoadQuality))] RoadQuality Road,
    [property: JsonRequired] bool River,
    [property: JsonRequired] bool Bridge,
    [property: JsonRequired, EnumDataType(typeof(Waterway))] Waterway Waterway
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
