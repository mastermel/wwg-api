using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// A unit's forced marches (decision 0018), as of the end of a turn: its run of moving turns, and
/// how many turns of forced march it has to rest off.
/// </summary>
/// <param name="MovesInRow">Moving turns in its run, before the first forced march is made.</param>
/// <param name="ForceMarchesInRow">Of those, the force-march orders.</param>
/// <param name="ForcedMarchTurns">
/// Turns of forced march still to rest off (the free first one counts as one); each moving turn
/// adds one once there's one, each Hold takes one off.
/// </param>
internal sealed record MarchState(int MovesInRow, int ForceMarchesInRow, int ForcedMarchTurns)
{
    public static readonly MarchState Rested = new(0, 0, 0);

    /// <summary>
    /// After a turn: a move adds to the run (the third move, or the second force march, makes the
    /// first forced march; each move after it is another turn of one), a Hold rests one turn off.
    /// </summary>
    public MarchState After(bool moved, bool forceMarch)
    {
        if (!moved)
        {
            return ForcedMarchTurns > 0
                ? this with
                {
                    ForcedMarchTurns = ForcedMarchTurns - 1,
                }
                : Rested;
        }

        if (ForcedMarchTurns > 0)
        {
            return this with { ForcedMarchTurns = ForcedMarchTurns + 1 };
        }

        var moves = MovesInRow + 1;
        var forceMarches = ForceMarchesInRow + (forceMarch ? 1 : 0);
        return moves >= 3 || forceMarches >= 2 ? new(0, 0, 1) : new(moves, forceMarches, 0);
    }

    /// <summary>
    /// The attrition a turn costs that leaves the unit at this state, as a multiple of the scale's:
    /// none for the first turn of forced march, then 1, 2, 4, 8…
    /// </summary>
    public int Multiplier => ForcedMarchTurns < 2 ? 0 : 1 << Math.Min(ForcedMarchTurns - 2, 20);
}

/// <summary>A unit's turn as marches count it.</summary>
internal sealed record MarchTurn(Guid UnitId, int Turn, bool Moved, bool ForceMarch);

internal static class Marches
{
    /// <summary>
    /// Each unit's march state at the end of the turns given (in order), from its orders in them;
    /// a turn without one is a turn of rest.
    /// </summary>
    public static Dictionary<Guid, MarchState> Replay(
        IEnumerable<Guid> unitIds,
        IReadOnlyList<int> turns,
        IEnumerable<MarchTurn> orders
    )
    {
        var byTurn = orders.ToLookup(o => o.UnitId);
        return unitIds.ToDictionary(
            id => id,
            id =>
            {
                var given = byTurn[id].ToDictionary(o => o.Turn);
                return turns.Aggregate(
                    MarchState.Rested,
                    (state, turn) =>
                        given.TryGetValue(turn, out var order)
                            ? state.After(order.Moved, order.ForceMarch)
                            : state.After(moved: false, forceMarch: false)
                );
            }
        );
    }

    /// <summary>
    /// The march states of the campaign's units (or one army's) as of the open turn's start: their
    /// orders in every turn closed since setup. Also the open turn's orders, for what they'd cost.
    /// </summary>
    public static async Task<(
        Dictionary<Guid, MarchState> Before,
        Dictionary<Guid, MarchTurn> Open
    )> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid? armyId,
        CancellationToken cancellationToken
    )
    {
        var turns = await db
            .CampaignTurns.AsNoTracking()
            .Where(t => t.CampaignId == campaignId && t.Number > 0)
            .OrderBy(t => t.Number)
            .Select(t => new { t.Number, Closed = t.ClosedAt != null })
            .ToListAsync(cancellationToken);
        var unitIds = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.CampaignId == campaignId && (armyId == null || u.ArmyId == armyId))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        var orders = (
            await db
                .UnitOrders.AsNoTracking()
                .Where(o =>
                    o.ArmyUnit.CampaignId == campaignId
                    && (armyId == null || o.ArmyUnit.ArmyId == armyId)
                    && o.ArmyTurn.CampaignTurn.Number > 0
                )
                .Select(o => new
                {
                    o.UnitId,
                    Turn = o.ArmyTurn.CampaignTurn.Number,
                    o.Kind,
                    o.Path,
                    o.Progress,
                    o.ForceMarch,
                    o.Boats,
                    o.CarrierId,
                })
                .ToListAsync(cancellationToken)
        )
            .Select(o => new MarchTurn(
                o.UnitId,
                o.Turn,
                // Any move, even part of the way into a hex; but not on boats, which is rest
                // (decision 0022), for the unit carried and its boats alike.
                o.Kind == OrderKind.Move
                    && (o.Path.Count > 0 || o.Progress != null)
                    && o.Boats.Count == 0
                    && o.CarrierId == null,
                o.ForceMarch
            ))
            .ToList();
        var closed = turns.Where(t => t.Closed).Select(t => t.Number).ToList();
        var open = turns.FirstOrDefault(t => !t.Closed)?.Number;
        return (
            Replay(unitIds, closed, orders),
            orders.Where(o => o.Turn == open).ToDictionary(o => o.UnitId)
        );
    }
}
