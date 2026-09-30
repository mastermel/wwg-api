using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Maps;

/// <summary>A campaign's terrain on its hex grid (decision 0014).</summary>
internal static class GridEndpoints
{
    public static IEndpointRouteBuilder MapGridEndpoints(this IEndpointRouteBuilder app)
    {
        var grid = app.MapGroup("/api/campaigns/{id:guid}/grid").WithTags("Maps");
        grid.MapGet("", GetCampaignGridAsync)
            .WithName("GetCampaignGrid")
            .RequireCampaignAccess(CampaignAccess.Member);
        grid.MapPut("", SaveCampaignGridAsync)
            .WithName("SaveCampaignGrid")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);
        grid.MapPut("/cells/{q:int}/{r:int}", UpdateHexCellAsync)
            .WithName("UpdateHexCell")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        grid.MapPut("/edges/{q:int}/{r:int}/{side}", UpdateHexEdgeAsync)
            .WithName("UpdateHexEdge")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>
    /// The campaign's terrain (every member): the hexes and edges with something on them. Empty
    /// until inferred or set.
    /// </summary>
    internal static async Task<Ok<CampaignGridResponse>> GetCampaignGridAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var cells = await db
            .HexCells.AsNoTracking()
            .Where(c => c.CampaignId == id)
            .OrderBy(c => c.Q)
            .ThenBy(c => c.R)
            .Select(c => new HexCellResponse(
                c.Q,
                c.R,
                c.Terrain,
                c.Forest,
                new HexSettlement(c.SettlementSize, c.Walled, c.Fortress, c.Capital, c.Name),
                c.SetByUmpire
            ))
            .ToListAsync(cancellationToken);
        var edges = await db
            .HexEdges.AsNoTracking()
            .Where(e => e.CampaignId == id)
            .OrderBy(e => e.Q)
            .ThenBy(e => e.R)
            .ThenBy(e => e.Side)
            .Select(e => new HexEdgeResponse(
                e.Q,
                e.R,
                e.Side,
                e.Road,
                e.River,
                e.Bridge,
                e.Waterway,
                e.SetByUmpire
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(new CampaignGridResponse(cells, edges));
    }

    /// <summary>
    /// Saves the terrain inferred for the whole grid (Umpire or Admin): it replaces what inference
    /// saved before, and the hexes and edges the Umpire set stay as they are. 409 without an area.
    /// </summary>
    internal static async Task<
        Results<NoContent, ValidationProblem, ProblemHttpResult>
    > SaveCampaignGridAsync(
        Guid id,
        SaveCampaignGridRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await CampaignMaps.GridAsync(db, id, cancellationToken) is not { } grid)
        {
            return NoArea();
        }

        if (Invalid(grid, request) is { } problem)
        {
            return problem;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db
            .HexCells.Where(c => c.CampaignId == id && !c.SetByUmpire)
            .ExecuteDeleteAsync(cancellationToken);
        await db
            .HexEdges.Where(e => e.CampaignId == id && !e.SetByUmpire)
            .ExecuteDeleteAsync(cancellationToken);
        var umpireCells = (
            await db
                .HexCells.Where(c => c.CampaignId == id)
                .Select(c => new { c.Q, c.R })
                .ToListAsync(cancellationToken)
        )
            .Select(c => new Hex(c.Q, c.R))
            .ToHashSet();
        var umpireEdges = (
            await db
                .HexEdges.Where(e => e.CampaignId == id)
                .Select(e => new
                {
                    e.Q,
                    e.R,
                    e.Side,
                })
                .ToListAsync(cancellationToken)
        ).Select(e => (new Hex(e.Q, e.R), e.Side)).ToHashSet();

        db.HexCells.AddRange(NewCells(id, request.Cells, umpireCells));
        db.HexEdges.AddRange(NewEdges(id, request.Edges, umpireEdges));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// The Umpire sets a hex's terrain (Umpire or Admin); inference leaves it alone from then on.
    /// 404 for a hex outside the grid, 409 without an area.
    /// </summary>
    internal static async Task<
        Results<Ok<HexCellResponse>, ValidationProblem, ProblemHttpResult>
    > UpdateHexCellAsync(
        Guid id,
        int q,
        int r,
        UpdateHexCellRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await CampaignMaps.GridAsync(db, id, cancellationToken) is not { } grid)
        {
            return NoArea();
        }

        if (!grid.Contains(new Hex(q, r)))
        {
            return NoSuch("hex");
        }

        if (request.Settlement.Problem() is { } wrong)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["settlement"] = [wrong],
                }
            );
        }

        var cell = await db.HexCells.SingleOrDefaultAsync(
            c => c.CampaignId == id && c.Q == q && c.R == r,
            cancellationToken
        );
        if (cell is null)
        {
            cell = new HexCell
            {
                CampaignId = id,
                Q = q,
                R = r,
            };
            db.HexCells.Add(cell);
        }

        Apply(cell, request.Terrain, request.Forest, request.Settlement);
        cell.SetByUmpire = true;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(
            new HexCellResponse(q, r, cell.Terrain, cell.Forest, SettlementOf(cell), true)
        );
    }

    /// <summary>
    /// The Umpire sets an edge's road, river and waterway (Umpire or Admin); inference leaves it alone from
    /// then on. The edge is on hex (q, r)'s N, NE or SE side; 404 unless it touches the grid, 409
    /// without an area.
    /// </summary>
    internal static async Task<
        Results<Ok<HexEdgeResponse>, ValidationProblem, ProblemHttpResult>
    > UpdateHexEdgeAsync(
        Guid id,
        int q,
        int r,
        EdgeSide side,
        UpdateHexEdgeRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await CampaignMaps.GridAsync(db, id, cancellationToken) is not { } grid)
        {
            return NoArea();
        }

        if (!Enum.IsDefined(side) || !Touches(grid, new Hex(q, r), side))
        {
            return NoSuch("edge");
        }

        if (request is { Bridge: true, River: false })
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["bridge"] = [BridgeNeedsARiver],
                }
            );
        }

        var edge = await db.HexEdges.SingleOrDefaultAsync(
            e => e.CampaignId == id && e.Q == q && e.R == r && e.Side == side,
            cancellationToken
        );
        if (edge is null)
        {
            edge = new HexEdge
            {
                CampaignId = id,
                Q = q,
                R = r,
                Side = side,
            };
            db.HexEdges.Add(edge);
        }

        (edge.Road, edge.River, edge.Bridge, edge.Waterway, edge.SetByUmpire) = (
            request.Road,
            request.River,
            request.Bridge,
            request.Waterway,
            true
        );
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(
            new HexEdgeResponse(q, r, side, edge.Road, edge.River, edge.Bridge, edge.Waterway, true)
        );
    }

    /// <summary>
    /// Deletes the campaign's terrain (not saved until the caller saves): it belongs to the old
    /// grid once the area or hex size changes.
    /// </summary>
    public static async Task ForgetAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        db.HexCells.RemoveRange(
            await db.HexCells.Where(c => c.CampaignId == campaignId).ToListAsync(cancellationToken)
        );
        db.HexEdges.RemoveRange(
            await db.HexEdges.Where(e => e.CampaignId == campaignId).ToListAsync(cancellationToken)
        );
    }

    // Only what has something on it is stored: anything else reads as Flat, or no road.

    private static IEnumerable<HexCell> NewCells(
        Guid campaignId,
        IEnumerable<InferredHexCell> cells,
        HashSet<Hex> setByUmpire
    ) =>
        cells
            .Where(c =>
                !setByUmpire.Contains(new Hex(c.Q, c.R))
                && (c.Terrain != Terrain.Flat || c.Forest || c.Settlement.IsAny)
            )
            .Select(c =>
            {
                var cell = new HexCell
                {
                    CampaignId = campaignId,
                    Q = c.Q,
                    R = c.R,
                };
                Apply(cell, c.Terrain, c.Forest, c.Settlement);
                return cell;
            });

    private static IEnumerable<HexEdge> NewEdges(
        Guid campaignId,
        IEnumerable<InferredHexEdge> edges,
        HashSet<(Hex, EdgeSide)> setByUmpire
    ) =>
        edges
            .Where(e =>
                !setByUmpire.Contains((new Hex(e.Q, e.R), e.Side))
                && (e.Road != RoadQuality.None || e.River || e.Waterway != Waterway.None)
            )
            .Select(e => new HexEdge
            {
                CampaignId = campaignId,
                Q = e.Q,
                R = e.R,
                Side = e.Side,
                Road = e.Road,
                River = e.River,
                Bridge = e.Bridge,
                Waterway = e.Waterway,
            });

    private static void Apply(
        HexCell cell,
        Terrain terrain,
        bool forest,
        HexSettlement settlement
    ) =>
        (
            cell.Terrain,
            cell.Forest,
            cell.SettlementSize,
            cell.Walled,
            cell.Fortress,
            cell.Capital,
            cell.Name
        ) = (
            terrain,
            forest,
            settlement.Size,
            settlement.Walled,
            settlement.Fortress,
            settlement.Capital,
            string.IsNullOrWhiteSpace(settlement.Name) ? null : settlement.Name
        );

    private static HexSettlement SettlementOf(HexCell cell) =>
        new(cell.SettlementSize, cell.Walled, cell.Fortress, cell.Capital, cell.Name);

    private const string BridgeNeedsARiver = "A bridge needs a river to cross.";

    /// <summary>The first broken rule of each list, as a validation problem; null if none.</summary>
    private static ValidationProblem? Invalid(HexGrid grid, SaveCampaignGridRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var cells = new HashSet<Hex>();
        foreach (var cell in request.Cells)
        {
            var hex = new Hex(cell.Q, cell.R);
            if (!grid.Contains(hex))
            {
                errors["cells"] = [$"Hex ({cell.Q}, {cell.R}) is outside the grid."];
                break;
            }

            if (!cells.Add(hex))
            {
                errors["cells"] = [$"Hex ({cell.Q}, {cell.R}) is there twice."];
                break;
            }

            if (cell.Settlement.Problem() is { } wrong)
            {
                errors["cells"] = [$"Hex ({cell.Q}, {cell.R}): {wrong}"];
                break;
            }
        }

        var edges = new HashSet<(Hex, EdgeSide)>();
        foreach (var edge in request.Edges)
        {
            var hex = new Hex(edge.Q, edge.R);
            var message =
                !Touches(grid, hex, edge.Side) ? "doesn't touch the grid"
                : !edges.Add((hex, edge.Side)) ? "is there twice"
                : edge is { Bridge: true, River: false } ? "has a bridge but no river"
                : null;
            if (message is not null)
            {
                errors["edges"] = [$"The edge on ({edge.Q}, {edge.R}) {edge.Side} {message}."];
                break;
            }
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    /// <summary>Whether an edge is on the grid: the hex it's stored on, or the one across it, is.</summary>
    private static bool Touches(HexGrid grid, Hex hex, EdgeSide side) =>
        grid.Contains(hex) || grid.Contains(Across(hex, side));

    /// <summary>The hex on the other side of one of a hex's N, NE or SE edges.</summary>
    private static Hex Across(Hex hex, EdgeSide side) =>
        side switch
        {
            EdgeSide.N => new Hex(hex.Q, hex.R - 1),
            EdgeSide.NE => new Hex(hex.Q + 1, hex.R - 1),
            _ => new Hex(hex.Q + 1, hex.R),
        };

    private static ProblemHttpResult NoArea() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "No map area",
            detail: "Set the map's area first: the grid is laid over it."
        );

    private static ProblemHttpResult NoSuch(string what) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: $"No such {what}",
            detail: $"That {what} isn't on the campaign's grid."
        );
}
