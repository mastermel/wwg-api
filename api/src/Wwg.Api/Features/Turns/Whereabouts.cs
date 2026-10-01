using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Turns;

/// <summary>A unit where a turn leaves it, with what the rules on it need.</summary>
internal sealed record UnitPlace(
    Guid UnitId,
    Guid ArmyId,
    Guid SideId,
    string Name,
    UnitType Type,
    int Points,
    Hex At,
    bool LivesOffTheLand
);

/// <summary>
/// Where a campaign's units are (supply, sight): where they ended the last closed turn (now), and
/// where the open turn's orders as given leave them (next). A unit not yet placed is in neither.
/// </summary>
internal static class Whereabouts
{
    private sealed record OrderRow(
        Guid UnitId,
        int Number,
        bool Closed,
        int Q,
        int R,
        bool LivesOffTheLand
    );

    public static async Task<(List<UnitPlace> Now, List<UnitPlace> Next)> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var units = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.CampaignId == campaignId)
            .Select(u => new
            {
                u.Id,
                u.ArmyId,
                u.Army.SideId,
                u.Name,
                u.Type,
                u.Points,
            })
            .ToListAsync(cancellationToken);
        var orders = await db
            .UnitOrders.AsNoTracking()
            .Where(o => o.ArmyUnit.CampaignId == campaignId)
            .Select(o => new OrderRow(
                o.UnitId,
                o.ArmyTurn.CampaignTurn.Number,
                o.ArmyTurn.CampaignTurn.ClosedAt != null,
                o.Q,
                o.R,
                o.LivesOffTheLand
            ))
            .ToListAsync(cancellationToken);
        var latest = orders
            .Where(o => o.Closed)
            .GroupBy(o => o.UnitId)
            .ToDictionary(g => g.Key, g => g.MaxBy(o => o.Number));
        var open = orders.Where(o => !o.Closed).ToDictionary(o => o.UnitId);
        List<UnitPlace> At(Func<Guid, OrderRow?> orderOf) =>
            [
                .. units.SelectMany(u =>
                    orderOf(u.Id) is { } order
                        ?
                        [
                            new UnitPlace(
                                u.Id,
                                u.ArmyId,
                                u.SideId,
                                u.Name,
                                u.Type,
                                u.Points,
                                new Hex(order.Q, order.R),
                                order.LivesOffTheLand
                            ),
                        ]
                        : Array.Empty<UnitPlace>()
                ),
            ];
        return (
            At(id => latest.GetValueOrDefault(id)),
            At(id => open.GetValueOrDefault(id) ?? latest.GetValueOrDefault(id))
        );
    }
}
