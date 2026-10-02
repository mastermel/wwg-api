using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Victory;

/// <summary>A settlement on the map: where, its name, and its worth.</summary>
internal sealed record Settlement(Hex At, string? Name, int Value);

/// <summary>Who holds the settlements, and how they change hands (step 50, decision 0021).</summary>
internal static class Holdings
{
    /// <summary>The campaign's settlements worth anything, by hex.</summary>
    public static async Task<Dictionary<Hex, Settlement>> SettlementsAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var mode = await db
            .Campaigns.Where(c => c.Id == campaignId)
            .Select(c => c.VictoryPoints)
            .SingleAsync(cancellationToken);
        return (
            await db
                .HexCells.AsNoTracking()
                .Where(c =>
                    c.CampaignId == campaignId
                    && (c.SettlementSize != SettlementSize.None || c.Fortress)
                )
                .ToListAsync(cancellationToken)
        )
            .Select(c => new Settlement(
                new Hex(c.Q, c.R),
                c.Name,
                GridEndpoints.SettlementOf(c).ValueIn(mode)
            ))
            .Where(s => s.Value > 0)
            .ToDictionary(s => s.At);
    }

    /// <summary>
    /// Settlements change hands as a turn closes (not saved): an army whose side alone has units in
    /// the hex takes it (of several, the one with the most points there); with both sides there,
    /// or none, it stays with its holder. Units on boats, and their boats, take nothing until they
    /// land (decision 0022), though they keep an enemy from taking it.
    /// </summary>
    public static async Task CloseTurnAsync(
        WwgDbContext db,
        Guid campaignId,
        int closing,
        CancellationToken cancellationToken
    )
    {
        var settlements = await SettlementsAsync(db, campaignId, cancellationToken);
        if (settlements.Count == 0)
        {
            return;
        }

        var (_, next) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        var held = await db
            .Holdings.Where(h => h.CampaignId == campaignId)
            .ToDictionaryAsync(h => new Hex(h.Q, h.R), cancellationToken);
        foreach (var hex in next.Where(p => settlements.ContainsKey(p.At)).GroupBy(p => p.At))
        {
            if (hex.Select(p => p.SideId).Distinct().Count() != 1)
            {
                continue;
            }

            var ashore = hex.Where(p => !p.Afloat).ToList();
            if (ashore.Count == 0)
            {
                continue;
            }

            var taker = ashore
                .GroupBy(p => p.ArmyId)
                .OrderByDescending(g => g.Sum(p => p.Points))
                .ThenBy(g => g.Key)
                .First()
                .Key;
            Hand(db, campaignId, held, hex.Key, taker, closing, byUmpire: false);
        }
    }

    /// <summary>Gives a settlement to an army, or no one, keeping the change (not saved).</summary>
    public static void Hand(
        WwgDbContext db,
        Guid campaignId,
        Dictionary<Hex, Holding> held,
        Hex at,
        Guid? armyId,
        int turn,
        bool byUmpire
    )
    {
        var holding = held.GetValueOrDefault(at);
        if (holding?.ArmyId == armyId || (holding is null && armyId is null))
        {
            return;
        }

        db.HoldingChanges.Add(
            new HoldingChange
            {
                CampaignId = campaignId,
                Turn = turn,
                Q = at.Q,
                R = at.R,
                FromArmyId = holding?.ArmyId,
                ToArmyId = armyId,
                ByUmpire = byUmpire,
            }
        );
        if (holding is null)
        {
            holding = db
                .Holdings.Add(
                    new Holding
                    {
                        CampaignId = campaignId,
                        Q = at.Q,
                        R = at.R,
                    }
                )
                .Entity;
            held[at] = holding;
        }

        holding.ArmyId = armyId;
    }
}
