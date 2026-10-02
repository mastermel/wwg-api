using System.Runtime.InteropServices;
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
            UnitType.Boat => MovementClass.Boat,
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

        if (movementClass == MovementClass.Boat)
        {
            return BoatStep(table, terrain, from, to);
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

    /// <summary>
    /// A boat's step (decision 0016): along a waterway across the edge, downstream or upstream
    /// by the way it flows; or onto a lake (a Water hex) from water or a waterway. Nowhere else.
    /// </summary>
    private static StepCost BoatStep(MovementTable table, PathTerrain terrain, Hex from, Hex to)
    {
        var edge = terrain.Edge(from, to);
        // The edge's flow is from the hex it's stored on; stored on `to`, it's turned round.
        var (stored, _) = PathTerrain.Stored(from, to);
        var ground = edge?.Waterway switch
        {
            Waterway.Out => stored == from ? Ground.Downstream : Ground.Upstream,
            Waterway.In => stored == from ? Ground.Upstream : Ground.Downstream,
            _ when terrain.Cell(to).Terrain == Terrain.Water
                    && terrain.Cell(from).Terrain == Terrain.Water => Ground.Lake,
            _ => (Ground?)null,
        };
        var rate = ground is { } water ? table.Rate(MovementClass.Boat, water) : 0;
        return rate > 0
            ? new StepCost(1 / rate, null)
            : StepCost.Closed("boats keep to waterways and lakes");
    }

    /// <summary>
    /// A turn's movement for a unit (step 45; the rules, §E.1): the turn's 1, and for infantry
    /// (the Infantry class) of a nation the campaign names, a flat hex's worth more each Morning
    /// or less each Afternoon.
    /// </summary>
    public static double BudgetFor(
        MovementTable table,
        MovementClass movementClass,
        Nation nation,
        TurnPart? part,
        Campaign calendar
    )
    {
        var flat = table.Rate(MovementClass.Infantry, Ground.Flat);
        if (movementClass != MovementClass.Infantry || flat <= 0)
        {
            return Budget;
        }

        var morning = calendar.MorningNations ?? TurnParts.MorningNations;
        var afternoon = calendar.AfternoonNations ?? TurnParts.AfternoonNations;
        return part switch
        {
            TurnPart.Morning when morning.Contains(nation) => Budget + (1 / flat),
            TurnPart.Afternoon when afternoon.Contains(nation) => Math.Max(0, Budget - (1 / flat)),
            _ => Budget,
        };
    }

    /// <summary>Whether a cost is within a turn's budget.</summary>
    public static bool Affordable(double cost) => cost <= Budget + Tolerance;

    /// <summary>
    /// Where a turn's move leaves a unit (step 44): each step paid from the turn's budget in turn.
    /// A last step into a hex that costs more than a whole turn takes what's left, and the unit
    /// stays short of it, part of the way in; next turn, a move on into the same hex starts from
    /// there (<paramref name="carried"/>). Any other step past the budget is too far.
    /// </summary>
    public static MovePlan Plan(
        MovementTable table,
        PathTerrain terrain,
        MovementClass movementClass,
        Hex start,
        IReadOnlyList<Hex> path,
        UnitState? carried,
        double budget = Budget
    )
    {
        var at = start;
        for (var i = 0; i < path.Count; i++)
        {
            var step = Step(table, terrain, movementClass, at, path[i]);
            if (step.ClosedBecause is { } closed)
            {
                return MovePlan.Refused($"The unit can't go that way: {closed}.");
            }

            // On into the hex it's part of the way into: only the rest to go.
            var already = i == 0 && carried?.Toward == path[i] ? carried.Value.Progress : 0;
            var cost = step.Cost * (1 - already);
            if (cost <= budget + Tolerance)
            {
                budget -= cost;
                at = path[i];
                continue;
            }

            return i == path.Count - 1 && step.Cost > Budget && budget > Tolerance
                ? new MovePlan(at, already + (budget / step.Cost), null)
                : MovePlan.Refused("That's further than the unit can move in a turn.");
        }

        return new MovePlan(at, null, null);
    }

    private static string ClassLabel(MovementClass movementClass) =>
        movementClass switch
        {
            MovementClass.Infantry => "infantry",
            MovementClass.Light => "light infantry",
            MovementClass.LightCavalry => "light cavalry",
            MovementClass.Cavalry => "cavalry",
            MovementClass.Boat => "boats",
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

/// <summary>
/// Where a move leaves a unit: in <paramref name="At"/>, and <paramref name="Progress"/> of the way
/// into the path's last hex when it didn't get there; or why it's refused.
/// </summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct MovePlan(Hex At, double? Progress, string? Problem)
{
    public static MovePlan Refused(string problem) => new(default, null, problem);
}

/// <summary>Where a unit is after its last turn, and how far into the hex it was heading for.</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct UnitState(Hex At, Hex? Toward, double Progress);

/// <summary>A step's (or path's) cost in turns, or why it's closed (then the cost is infinite).</summary>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct StepCost(double Cost, string? ClosedBecause)
{
    public static StepCost Closed(string because) => new(double.PositiveInfinity, because);
}

/// <summary>A campaign's movement table: its own rates, or the rule book's (§E.1).</summary>
internal sealed class MovementTable
{
    private readonly Dictionary<(MovementClass, Ground), double> _rates;

    private MovementTable(Dictionary<(MovementClass, Ground), double> rates) => _rates = rates;

    // Before Rules, which is built from them (static fields start in order).
    private static readonly Ground[] Land =
    [
        Ground.GoodRoad,
        Ground.PoorRoad,
        Ground.Flat,
        Ground.LowHill,
        Ground.HighHill,
        Ground.Mountain,
    ];

    private static readonly Ground[] Water = [Ground.Downstream, Ground.Upstream, Ground.Lake];

    /// <summary>
    /// The rule book's table, in hexes a turn: for land units good road, poor road, flat, low
    /// hill, high hill, mountain; for boats downstream, upstream, lake.
    /// </summary>
    public static readonly IReadOnlyDictionary<(MovementClass, Ground), double> Rules = Table(
        (MovementClass.Infantry, [3, 2, 2, 1, 0.5, 0]),
        (MovementClass.Light, [5, 4, 3, 2, 1, 0.5]),
        (MovementClass.LightCavalry, [6, 5, 4, 3, 2, 1]),
        (MovementClass.Cavalry, [5, 4, 3, 2, 1, 0]),
        (MovementClass.Slow, [3, 2, 1, 0.5, 0, 0]),
        (MovementClass.Boat, [4, 2, 3])
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

    /// <summary>Each class's rates on its grounds: boats on water, the rest on land.</summary>
    private static Dictionary<(MovementClass, Ground), double> Table(
        params (MovementClass Class, double[] Rates)[] rows
    ) =>
        rows.SelectMany(row =>
                (row.Class == MovementClass.Boat ? Water : Land).Select(
                    (ground, i) => ((row.Class, ground), row.Rates[i])
                )
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

    /// <summary>
    /// The terrain of the campaign's whole grid: for a search that may go anywhere in it (a
    /// courier's ride). Only hexes and edges with something on them are stored.
    /// </summary>
    public static async Task<PathTerrain> LoadAllAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var cells = await db
            .HexCells.AsNoTracking()
            .Where(c => c.CampaignId == campaignId)
            .ToDictionaryAsync(
                c => new Hex(c.Q, c.R),
                c => (c.Terrain, c.Forest),
                cancellationToken
            );
        var edges = await db
            .HexEdges.AsNoTracking()
            .Where(e => e.CampaignId == campaignId)
            .ToDictionaryAsync(e => (new Hex(e.Q, e.R), e.Side), cancellationToken);
        return new PathTerrain(cells, edges);
    }
}
