using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Supply;

/// <summary>Where a unit stands for supply (decision 0019).</summary>
public enum SupplyState
{
    /// <summary>Within reach of a route from one of its army's depots.</summary>
    Supplied,

    /// <summary>Cut off from its army's depots: attrition from the 7th turn in a row.</summary>
    Unsupplied,

    /// <summary>Its type doesn't need supply (the campaign's exempt types).</summary>
    Exempt,

    /// <summary>Living off the land: it can't be out of supply.</summary>
    LivingOffTheLand,

    /// <summary>Its army has no depots: its supply isn't tracked.</summary>
    Untracked,
}

/// <summary>A unit as supply sees it: where it is, its side, and what it needs.</summary>
internal sealed record SupplyUnit(
    Guid UnitId,
    Guid ArmyId,
    Guid SideId,
    Hex At,
    UnitType Type,
    int Points,
    bool LivesOffTheLand
);

/// <summary>A depot as supply sees it.</summary>
internal sealed record SupplyDepot(Guid Id, Guid ArmyId, Hex At, DepotKind Kind, int CutOffTurns);

/// <summary>What supply needs besides the units and depots.</summary>
/// <param name="Routes">Each hex's neighbours by road or waterway.</param>
/// <param name="ArmySides">Each army's side.</param>
/// <param name="Reach">How far from a route a unit stays supplied, in hexes.</param>
/// <param name="ExemptTypes">The unit types that don't need supply.</param>
internal sealed record SupplyMap(
    IReadOnlyDictionary<Hex, List<Hex>> Routes,
    IReadOnlyDictionary<Guid, Guid> ArmySides,
    int Reach,
    IReadOnlyCollection<UnitType> ExemptTypes
);

/// <summary>A unit's supply: its state, and the depot it's supplied from.</summary>
internal sealed record UnitSupply(SupplyState State, Guid? DepotId);

/// <summary>
/// Supply (the rules, §G; decision 0019): each army's routes by road and waterway from its own
/// depots, cut by a hex holding 5 or more points of the other side unless the army's side has
/// twice as many there; a unit is supplied within the reach of a hex its routes come to.
/// </summary>
internal static class SupplyLines
{
    /// <summary>The fewest enemy points in a hex that cut a route through it (§G.3).</summary>
    public const int CuttingPoints = 5;

    /// <summary>How long an intermediate depot's stock lasts once cut off, in turns (§G.2(b)).</summary>
    public const int IntermediateStockTurns = 15;

    /// <summary>
    /// Each unit's supply, and whether each intermediate depot has a route to a main one.
    /// </summary>
    public static (Dictionary<Guid, UnitSupply> Units, Dictionary<Guid, bool> Connected) Of(
        SupplyMap map,
        IReadOnlyList<SupplyUnit> units,
        IReadOnlyList<SupplyDepot> depots
    )
    {
        var points = units
            .GroupBy(u => (u.At, u.SideId))
            .ToDictionary(g => g.Key, g => g.Sum(u => u.Points));
        var totals = units.GroupBy(u => u.At).ToDictionary(g => g.Key, g => g.Sum(u => u.Points));
        bool Cut(Guid side, Hex hex)
        {
            var own = points.GetValueOrDefault((hex, side));
            var enemy = totals.GetValueOrDefault(hex) - own;
            return enemy >= CuttingPoints && own < 2 * enemy;
        }

        var connected = new Dictionary<Guid, bool>();
        var reached = new Dictionary<Guid, Dictionary<Hex, Guid>>();
        foreach (var army in depots.GroupBy(d => d.ArmyId))
        {
            var side = map.ArmySides.GetValueOrDefault(army.Key);
            bool Open(Hex hex) => !Cut(side, hex);
            var main = army.Where(d => d.Kind == DepotKind.Main && Open(d.At)).ToList();
            var fromMain = Spread(map.Routes, main, Open);
            var supplying = new List<SupplyDepot>(main);
            foreach (var depot in army.Where(d => d.Kind == DepotKind.Intermediate))
            {
                connected[depot.Id] = Open(depot.At) && fromMain.ContainsKey(depot.At);
                // Cut off, it goes on supplying from its stock for 15 turns.
                if (
                    Open(depot.At)
                    && (connected[depot.Id] || depot.CutOffTurns < IntermediateStockTurns)
                )
                {
                    supplying.Add(depot);
                }
            }

            reached[army.Key] = Spread(map.Routes, supplying, Open);
        }

        return (units.ToDictionary(u => u.UnitId, u => StateOf(map, u, reached)), connected);
    }

    private static UnitSupply StateOf(
        SupplyMap map,
        SupplyUnit unit,
        Dictionary<Guid, Dictionary<Hex, Guid>> reached
    )
    {
        if (map.ExemptTypes.Contains(unit.Type))
        {
            return new(SupplyState.Exempt, null);
        }
        if (unit.LivesOffTheLand)
        {
            return new(SupplyState.LivingOffTheLand, null);
        }
        if (!reached.TryGetValue(unit.ArmyId, out var routes))
        {
            return new(SupplyState.Untracked, null);
        }

        var depot = Within(unit.At, map.Reach)
            .Select(hex => routes.TryGetValue(hex, out var from) ? from : (Guid?)null)
            .FirstOrDefault(from => from is not null);
        return depot is null ? new(SupplyState.Unsupplied, null) : new(SupplyState.Supplied, depot);
    }

    /// <summary>
    /// Every hex the routes reach from the depots, through open hexes, each with the depot it's
    /// nearest (by steps).
    /// </summary>
    private static Dictionary<Hex, Guid> Spread(
        IReadOnlyDictionary<Hex, List<Hex>> routes,
        IEnumerable<SupplyDepot> from,
        Func<Hex, bool> open
    )
    {
        var reached = new Dictionary<Hex, Guid>();
        var queue = new Queue<Hex>();
        foreach (var depot in from)
        {
            if (reached.TryAdd(depot.At, depot.Id))
            {
                queue.Enqueue(depot.At);
            }
        }

        while (queue.TryDequeue(out var hex))
        {
            foreach (var next in routes.GetValueOrDefault(hex) ?? [])
            {
                if (open(next) && reached.TryAdd(next, reached[hex]))
                {
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }

    /// <summary>The hexes within <paramref name="reach"/> steps of a hex, nearest first.</summary>
    private static IEnumerable<Hex> Within(Hex centre, int reach)
    {
        var hexes = new List<Hex>();
        for (var q = -reach; q <= reach; q++)
        {
            for (var r = Math.Max(-reach, -q - reach); r <= Math.Min(reach, -q + reach); r++)
            {
                hexes.Add(new Hex(centre.Q + q, centre.R + r));
            }
        }

        return hexes.OrderBy(hex => hex.Distance(centre));
    }

    /// <summary>Each hex's neighbours by road or waterway, both ways (decision 0019).</summary>
    public static Dictionary<Hex, List<Hex>> RoutesOf(IEnumerable<HexEdge> edges)
    {
        var routes = new Dictionary<Hex, List<Hex>>();
        void Link(Hex from, Hex to)
        {
            if (!routes.TryGetValue(from, out var next))
            {
                routes[from] = next = [];
            }
            next.Add(to);
        }

        foreach (
            var edge in edges.Where(e => e.Road != RoadQuality.None || e.Waterway != Waterway.None)
        )
        {
            var hex = new Hex(edge.Q, edge.R);
            var across = GridEndpoints.Across(hex, edge.Side);
            Link(hex, across);
            Link(across, hex);
        }

        return routes;
    }
}
