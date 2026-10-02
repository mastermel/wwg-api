using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Intelligence;

/// <summary>
/// A courier's ride (the rules, §L; decision 0020): as light cavalry, by the campaign's movement
/// table over the terrain and roads, a turn's worth at a time, nights too.
/// </summary>
internal sealed class CourierRides
{
    private readonly HexGrid _grid;
    private readonly MovementTable _table;
    private readonly PathTerrain _terrain;
    private readonly MovementClass _class = Movement.ClassOf(UnitType.LightCavalry);

    private CourierRides(HexGrid grid, MovementTable table, PathTerrain terrain) =>
        (_grid, _table, _terrain) = (grid, table, terrain);

    /// <summary>
    /// What a ride needs: the campaign's table, and the terrain of the whole grid, which the
    /// cheapest way may cross anywhere (a ford round a river's end, say).
    /// </summary>
    public static async Task<CourierRides> LoadAsync(
        WwgDbContext db,
        HexGrid grid,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        new(
            grid,
            await MovementTable.LoadAsync(db, campaignId, cancellationToken),
            await PathTerrain.LoadAllAsync(db, campaignId, cancellationToken)
        );

    /// <summary>The cheapest way from a hex to every hex it reaches, in turns: each step's cost, and the hex before.</summary>
    private Dictionary<Hex, (double Cost, Hex? From)> Costs(Hex start)
    {
        var best = new Dictionary<Hex, (double Cost, Hex? From)> { [start] = (0, null) };
        var queue = new PriorityQueue<Hex, double>();
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var hex, out var cost))
        {
            if (cost > best[hex].Cost)
            {
                continue;
            }

            foreach (var direction in Hex.Directions)
            {
                var next = new Hex(hex.Q + direction.Q, hex.R + direction.R);
                if (!_grid.Contains(next))
                {
                    continue;
                }

                var step = Movement.Step(_table, _terrain, _class, hex, next);
                var total = cost + step.Cost;
                if (
                    step.ClosedBecause is null
                    && (!best.TryGetValue(next, out var known) || total < known.Cost)
                )
                {
                    best[next] = (total, hex);
                    queue.Enqueue(next, total);
                }
            }
        }

        return best;
    }

    /// <summary>How many turns' ride from one hex to another, or null if no way gets there.</summary>
    public double? TurnsTo(Hex from, Hex to) =>
        Costs(from).TryGetValue(to, out var found) ? found.Cost : null;

    /// <summary>
    /// Where a turn's ride from a hex towards another leaves the courier: along the cheapest way,
    /// as far as a turn goes. Where no way gets there, it waits.
    /// </summary>
    public Hex Ride(Hex from, Hex to)
    {
        var costs = Costs(from);
        if (!costs.ContainsKey(to))
        {
            return from;
        }

        var path = new List<Hex>();
        for (Hex? at = to; at is { } hex && hex != from; at = costs[hex].From)
        {
            path.Add(hex);
        }

        path.Reverse();
        return path.LastOrDefault(hex => Movement.Affordable(costs[hex].Cost), from);
    }
}
