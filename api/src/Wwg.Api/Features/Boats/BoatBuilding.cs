using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Boats;

/// <summary>
/// Building (or procuring) boats (decision 0022): a land unit in a settlement on a waterway, with
/// no enemy there, works two turns in a row, and a boat joins its army as the second closes.
/// </summary>
internal static class BoatBuilding
{
    /// <summary>The turns of work a boat takes.</summary>
    public const int Turns = 2;

    /// <summary>
    /// Why an army's unit can't build a boat in this hex, or null if it can: it needs a town, city or
    /// fortress with a waterway along one of its sides, and none of the other side's units there
    /// as the turn starts.
    /// </summary>
    public static async Task<string?> SiteProblemAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid armyId,
        Hex at,
        CancellationToken cancellationToken
    )
    {
        var settled = await db
            .HexCells.AsNoTracking()
            .AnyAsync(
                c =>
                    c.CampaignId == campaignId
                    && c.Q == at.Q
                    && c.R == at.R
                    && (c.SettlementSize != SettlementSize.None || c.Fortress),
                cancellationToken
            );
        if (!settled)
        {
            return "Boats are built in a town, city or fortress.";
        }

        var neighbours = Hex.Directions.Select(d => new Hex(at.Q + d.Q, at.R + d.R)).ToList();
        var terrain = await PathTerrain.LoadAsync(
            db,
            campaignId,
            [at, .. neighbours],
            cancellationToken
        );
        if (!neighbours.Any(n => terrain.Edge(at, n) is { Waterway: not Waterway.None }))
        {
            return "Boats are built where a waterway runs: this settlement has none.";
        }

        var side = await db
            .Armies.AsNoTracking()
            .Where(a => a.Id == armyId)
            .Select(a => a.SideId)
            .SingleAsync(cancellationToken);
        var (now, _) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        return now.Any(p => p.At == at && p.SideId != side)
            ? "The enemy is here: no boats can be built."
            : null;
    }

    /// <summary>
    /// As a turn closes (not saved): each unit finishing its second turn of work in a row (or its
    /// fourth, and so on) adds a boat to its army, in its hex, placed there in the closing turn.
    /// </summary>
    public static async Task CloseTurnAsync(
        WwgDbContext db,
        Guid campaignId,
        int closing,
        CancellationToken cancellationToken
    )
    {
        var work = await db
            .UnitOrders.AsNoTracking()
            .Where(o =>
                o.ArmyUnit.CampaignId == campaignId
                && o.Kind == OrderKind.BuildBoat
                && o.ArmyTurn.CampaignTurn.Number <= closing
            )
            .Select(o => new
            {
                o.UnitId,
                o.ArmyTurnId,
                o.ArmyTurn.ArmyId,
                Turn = o.ArmyTurn.CampaignTurn.Number,
                o.Q,
                o.R,
            })
            .ToListAsync(cancellationToken);
        var byUnit = work.ToLookup(w => w.UnitId, w => w.Turn);
        foreach (var done in work.Where(w => w.Turn == closing).OrderBy(w => w.UnitId))
        {
            var turns = byUnit[done.UnitId].ToHashSet();
            var run = 0;
            while (turns.Contains(closing - run))
            {
                run++;
            }

            if (run % Turns == 0)
            {
                await AddBoatAsync(
                    db,
                    campaignId,
                    done.ArmyId,
                    done.ArmyTurnId,
                    new Hex(done.Q, done.R),
                    cancellationToken
                );
            }
        }
    }

    /// <summary>A new boat in the army (not saved), named for the next number its boats haven't used.</summary>
    private static async Task AddBoatAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid armyId,
        Guid armyTurnId,
        Hex at,
        CancellationToken cancellationToken
    )
    {
        var names = (
            await db
                .ArmyUnits.AsNoTracking()
                .Where(u => u.ArmyId == armyId)
                .Select(u => u.Name)
                .ToListAsync(cancellationToken)
        )
            .Concat(db.ArmyUnits.Local.Where(u => u.ArmyId == armyId).Select(u => u.Name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var number = 1;
        while (names.Contains(Name(number)))
        {
            number++;
        }

        var boat = db
            .ArmyUnits.Add(
                new ArmyUnit
                {
                    ArmyId = armyId,
                    CampaignId = campaignId,
                    Name = Name(number),
                    Type = UnitType.Boat,
                    FightingFactor = UnitStats.MinFightingFactor,
                    Points = 0,
                }
            )
            .Entity;
        db.UnitOrders.Add(
            new UnitOrder
            {
                ArmyTurnId = armyTurnId,
                UnitId = boat.Id,
                Kind = OrderKind.Hold,
                Q = at.Q,
                R = at.R,
            }
        );
    }

    private static string Name(int number) => FormattableString.Invariant($"Boat {number}");
}
