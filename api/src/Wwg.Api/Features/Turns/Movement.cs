using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// How far units move in a turn (DESIGN.md §5.2, Phase 11; decision 0014): a turn's budget is 1,
/// and entering a hex costs 1 ÷ the class's rate for the step, in hexes per turn, by the
/// campaign's movement table. The front-end's movement.ts does the same; both are tested
/// against testdata/movement.json.
/// </summary>
internal static class Movement
{
    /// <summary>The most steps an order's path can have.</summary>
    public const int MaxSteps = 50;

    /// <summary>A turn's movement.</summary>
    public const double Budget = 1;

    // Sums of thirds don't come to exactly 1.
    private const double Tolerance = 1e-9;

    public static MovementClass ClassOf(UnitType type) =>
        type switch
        {
            UnitType.LineInfantry or UnitType.FootArtillery or UnitType.Engineers =>
                MovementClass.Infantry,
            UnitType.LightInfantry or UnitType.Partisans => MovementClass.Light,
            UnitType.LightCavalry or UnitType.Scouts => MovementClass.LightCavalry,
            UnitType.MediumCavalry or UnitType.HeavyCavalry or UnitType.HorseArtillery =>
                MovementClass.Cavalry,
            UnitType.SupplyTrain or UnitType.SiegeArtillery => MovementClass.Slow,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No movement class."),
        };

    /// <summary>
    /// What entering <paramref name="to"/> from <paramref name="from"/> costs, or why it can't be
    /// done. The ground is the hex's (a forest hex moves as low hills, or its own hills if
    /// higher), or the road across the edge when that's quicker (a good road into high hills or
    /// mountains counts as poor). Water, a river along the edge without a bridge, and ground the
    /// class can't cross (0 in the table) close the step.
    /// </summary>
    public static StepCost Step(
        MovementTable table,
        PathTerrain terrain,
        MovementClass movementClass,
        Hex from,
        Hex to
    )
    {
        if (!from.IsNextTo(to))
        {
            return StepCost.Closed("each step must be to the next hex");
        }

        var (ground, forest) = terrain.Cell(to);
        if (ground == Terrain.Water)
        {
            return StepCost.Closed("it's water");
        }

        var edge = terrain.Edge(from, to);
        if (edge is { River: true, Bridge: false })
        {
            return StepCost.Closed("a river without a bridge is in the way");
        }

        var land = ground switch
        {
            Terrain.Mountain => Ground.Mountain,
            Terrain.HighHill => Ground.HighHill,
            Terrain.LowHill => Ground.LowHill,
            _ when forest => Ground.LowHill,
            _ => Ground.Flat,
        };
        var road = edge?.Road switch
        {
            RoadQuality.Good when land is Ground.HighHill or Ground.Mountain => Ground.PoorRoad,
            RoadQuality.Good => Ground.GoodRoad,
            RoadQuality.Poor => Ground.PoorRoad,
            _ => (Ground?)null,
        };
        var rate = Math.Max(
            table.Rate(movementClass, land),
            road is { } onRoad ? table.Rate(movementClass, onRoad) : 0
        );
        return rate > 0
            ? new StepCost(1 / rate, null)
            : StepCost.Closed($"{ClassLabel(movementClass)} can't cross {GroundLabel(land)}");
    }

    /// <summary>What a path from <paramref name="start"/> costs, or its first closed step's reason.</summary>
    public static StepCost PathCost(
        MovementTable table,
        PathTerrain terrain,
        MovementClass movementClass,
        Hex start,
        IReadOnlyList<Hex> path
    )
    {
        var cost = 0.0;
        var at = start;
        foreach (var step in path)
        {
            var next = Step(table, terrain, movementClass, at, step);
            if (next.ClosedBecause is not null)
            {
                return next;
            }

            cost += next.Cost;
            at = step;
        }

        return new StepCost(cost, null);
    }

    /// <summary>Whether a cost is within a turn's budget.</summary>
    public static bool Affordable(double cost) => cost <= Budget + Tolerance;

    private static string ClassLabel(MovementClass movementClass) =>
        movementClass switch
        {
            MovementClass.Infantry => "infantry",
            MovementClass.Light => "light infantry",
            MovementClass.LightCavalry => "light cavalry",
            MovementClass.Cavalry => "cavalry",
            _ => "supply trains and siege artillery",
        };

    private static string GroundLabel(Ground ground) =>
        ground switch
        {
            Ground.Mountain => "mountains",
            Ground.HighHill => "high hills",
            Ground.LowHill => "low hills",
            _ => "that ground",
        };
}

/// <summary>A step's (or path's) cost in turns, or why it's closed (then the cost is infinite).</summary>
internal readonly record struct StepCost(double Cost, string? ClosedBecause)
{
    public static StepCost Closed(string because) => new(double.PositiveInfinity, because);
}

/// <summary>A campaign's movement table: its own rates, or the rule book's (§E.1).</summary>
internal sealed class MovementTable
{
    private readonly Dictionary<(MovementClass, Ground), double> _rates;

    private MovementTable(Dictionary<(MovementClass, Ground), double> rates) => _rates = rates;

    /// <summary>The rule book's table, in hexes a turn: good road, poor road, flat, low hill, high hill, mountain.</summary>
    public static readonly IReadOnlyDictionary<(MovementClass, Ground), double> Rules = Table(
        (MovementClass.Infantry, [3, 2, 2, 1, 0.5, 0]),
        (MovementClass.Light, [5, 4, 3, 2, 1, 0.5]),
        (MovementClass.LightCavalry, [6, 5, 4, 3, 2, 1]),
        (MovementClass.Cavalry, [5, 4, 3, 2, 1, 0]),
        (MovementClass.Slow, [3, 2, 1, 0.5, 0, 0])
    );

    public static readonly MovementTable TheRules = new(new(Rules));

    public double Rate(MovementClass movementClass, Ground ground) =>
        _rates.TryGetValue((movementClass, ground), out var rate) ? rate : 0;

    /// <summary>The campaign's table: the Umpire's rates where set, the rules' elsewhere.</summary>
    public static async Task<MovementTable> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var rates = new Dictionary<(MovementClass, Ground), double>(Rules);
        foreach (
            var row in await db
                .MovementRates.AsNoTracking()
                .Where(m => m.CampaignId == campaignId)
                .ToListAsync(cancellationToken)
        )
        {
            rates[(row.Class, row.Ground)] = row.Hexes;
        }

        return new MovementTable(rates);
    }

    private static Dictionary<(MovementClass, Ground), double> Table(
        params (MovementClass Class, double[] Rates)[] rows
    ) =>
        rows.SelectMany(row =>
                Enum.GetValues<Ground>().Select((ground, i) => ((row.Class, ground), row.Rates[i]))
            )
            .ToDictionary(pair => pair.Item1, pair => pair.Item2);
}

/// <summary>The terrain along a path: its hexes' ground and the edges between them.</summary>
internal sealed class PathTerrain
{
    private readonly Dictionary<Hex, (Terrain, bool)> _cells;
    private readonly Dictionary<(Hex, EdgeSide), HexEdge> _edges;

    private PathTerrain(
        Dictionary<Hex, (Terrain, bool)> cells,
        Dictionary<(Hex, EdgeSide), HexEdge> edges
    ) => (_cells, _edges) = (cells, edges);

    /// <summary>A hex's ground and forest (Flat and open where nothing's stored).</summary>
    public (Terrain Terrain, bool Forest) Cell(Hex hex) =>
        _cells.TryGetValue(hex, out var cell) ? cell : (Terrain.Flat, false);

    /// <summary>The edge between two neighbouring hexes, or null if nothing's on it.</summary>
    public HexEdge? Edge(Hex from, Hex to) => _edges.GetValueOrDefault(Stored(from, to));

    /// <summary>
    /// Where the edge between neighbours is stored: on the hex whose N, NE or SE side it is (the
    /// others are the neighbour's).
    /// </summary>
    public static (Hex, EdgeSide) Stored(Hex from, Hex to)
    {
        var direction = Hex.Directions.ToList().IndexOf(new Hex(to.Q - from.Q, to.R - from.R));
        return direction < 3 ? (from, (EdgeSide)direction) : (to, (EdgeSide)(direction - 3));
    }

    /// <summary>The terrain of these hexes and the edges among them.</summary>
    public static async Task<PathTerrain> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        IReadOnlyCollection<Hex> hexes,
        CancellationToken cancellationToken
    )
    {
        // Hexes of a path lie within a few columns and rows: fetch that box, keep the path's.
        var qs = hexes.Select(h => h.Q).ToList();
        var rs = hexes.Select(h => h.R).ToList();
        var (minQ, maxQ, minR, maxR) = (qs.Min() - 1, qs.Max() + 1, rs.Min() - 1, rs.Max() + 1);
        var wanted = hexes.ToHashSet();
        var cells = (
            await db
                .HexCells.AsNoTracking()
                .Where(c =>
                    c.CampaignId == campaignId
                    && c.Q >= minQ
                    && c.Q <= maxQ
                    && c.R >= minR
                    && c.R <= maxR
                )
                .Select(c => new
                {
                    c.Q,
                    c.R,
                    c.Terrain,
                    c.Forest,
                })
                .ToListAsync(cancellationToken)
        ).Where(c => wanted.Contains(new Hex(c.Q, c.R))).ToDictionary(c => new Hex(c.Q, c.R), c => (c.Terrain, c.Forest));
        var edges = (
            await db
                .HexEdges.AsNoTracking()
                .Where(e =>
                    e.CampaignId == campaignId
                    && e.Q >= minQ
                    && e.Q <= maxQ
                    && e.R >= minR
                    && e.R <= maxR
                )
                .ToListAsync(cancellationToken)
        ).ToDictionary(e => (new Hex(e.Q, e.R), e.Side));
        return new PathTerrain(cells, edges);
    }
}
