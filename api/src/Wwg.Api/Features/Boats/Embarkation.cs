using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Boats;

/// <summary>
/// Who is on boats (decision 0022): as the open turn starts (now, by the orders of the last
/// closed turn), and after its orders as given (next). Each unit aboard, with the boats tied to
/// it; a unit at 0 points has lost its boats, which are free again.
/// </summary>
internal sealed class Embarked
{
    private readonly Dictionary<Guid, Guid> _carrierNow;
    private readonly Dictionary<Guid, Guid> _carrierNext;

    public Embarked(
        Dictionary<Guid, IReadOnlyList<Guid>> now,
        Dictionary<Guid, IReadOnlyList<Guid>> next
    )
    {
        (Now, Next) = (now, next);
        _carrierNow = Carriers(now);
        _carrierNext = Carriers(next);
    }

    /// <summary>The units aboard as the open turn starts, and their boats.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Now { get; }

    /// <summary>The units aboard after the open turn's orders as given, and their boats.</summary>
    public IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> Next { get; }

    /// <summary>The unit a boat is tied to as the open turn starts, or null if it's free.</summary>
    public Guid? CarrierNow(Guid boatId) =>
        _carrierNow.TryGetValue(boatId, out var carrier) ? carrier : null;

    /// <summary>Whether a unit is on the water as the turn starts: aboard, or a boat tied to one.</summary>
    public bool AfloatNow(Guid unitId) =>
        Now.ContainsKey(unitId) || _carrierNow.ContainsKey(unitId);

    /// <summary>Whether a unit is on the water after the turn's orders.</summary>
    public bool AfloatNext(Guid unitId) =>
        Next.ContainsKey(unitId) || _carrierNext.ContainsKey(unitId);

    private static Dictionary<Guid, Guid> Carriers(Dictionary<Guid, IReadOnlyList<Guid>> aboard) =>
        aboard
            .SelectMany(a => a.Value.Select(boat => (Boat: boat, Carrier: a.Key)))
            .ToDictionary(t => t.Boat, t => t.Carrier);
}

internal static class Embarkation
{
    /// <summary>Whether an order leaves its unit on its boats: any but a landing, with boats.</summary>
    public static bool LeavesAboard(OrderKind kind, IReadOnlyCollection<Guid> boats) =>
        boats.Count > 0 && kind != OrderKind.Disembark;

    private sealed record Row(
        Guid UnitId,
        int Number,
        bool Closed,
        OrderKind Kind,
        List<Guid> Boats
    );

    /// <summary>Who is on boats in the campaign, now and after the open turn's orders.</summary>
    public static async Task<Embarked> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var lost = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.CampaignId == campaignId && u.Points == 0)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var orders = await db
            .UnitOrders.AsNoTracking()
            .Where(o => o.ArmyUnit.CampaignId == campaignId && o.ArmyTurn.CampaignTurn.Number > 0)
            .Select(o => new Row(
                o.UnitId,
                o.ArmyTurn.CampaignTurn.Number,
                o.ArmyTurn.CampaignTurn.ClosedAt != null,
                o.Kind,
                o.Boats
            ))
            .ToListAsync(cancellationToken);
        var latest = orders
            .Where(o => o.Closed)
            .GroupBy(o => o.UnitId)
            .Select(g => g.MaxBy(o => o.Number)!) // A group has at least one row.
            .ToList();
        var open = orders.Where(o => !o.Closed).ToDictionary(o => o.UnitId);

        Dictionary<Guid, IReadOnlyList<Guid>> Aboard(IEnumerable<Row> rows) =>
            rows.Where(o => !lost.Contains(o.UnitId) && LeavesAboard(o.Kind, o.Boats))
                .ToDictionary(o => o.UnitId, o => (IReadOnlyList<Guid>)o.Boats);

        var now = Aboard(latest);
        var next = Aboard(latest.Where(o => !open.ContainsKey(o.UnitId)).Concat(open.Values));
        return new Embarked(now, next);
    }

    /// <summary>
    /// Where a unit lands (an order to disembark, from <paramref name="from"/>), or why it can't:
    /// in its own hex, across a river side of it, or from a lake onto a shore beside it; never
    /// onto water.
    /// </summary>
    public static string? LandingProblem(
        Turns.PathTerrain terrain,
        Hex from,
        IReadOnlyList<Hex> path
    )
    {
        if (path.Count > 1)
        {
            return "Land in the unit's hex, or the next one: one step at most.";
        }

        var to = path.Count == 0 ? from : path[0];
        if (terrain.Cell(to).Terrain == Terrain.Water)
        {
            return "That's water: land on dry ground.";
        }

        if (path.Count == 1)
        {
            var acrossARiver = terrain.Edge(from, to) is { River: true };
            var offALake = terrain.Cell(from).Terrain == Terrain.Water;
            if (!acrossARiver && !offALake)
            {
                return "Land in the unit's hex, across a river side of it, or from a lake onto its shore.";
            }
        }

        return null;
    }
}
