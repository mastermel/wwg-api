using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Supply;

/// <summary>A campaign's supply as the open turn began, and as its orders as given would leave it.</summary>
internal sealed record CampaignSupply(
    Dictionary<Guid, UnitSupply> Now,
    Dictionary<Guid, UnitSupply> Next,
    Dictionary<Guid, bool> ConnectedNow,
    Dictionary<Guid, bool> ConnectedNext,
    Dictionary<Guid, (Guid ArmyId, int UnsuppliedTurns)> Units,
    List<Depot> Depots
);

/// <summary>Loads what supply needs (step 48c): units where they are, depots, routes, settings.</summary>
internal static class SupplyData
{
    public static async Task<CampaignSupply> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var map = await MapAsync(db, campaignId, cancellationToken);
        var units = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.CampaignId == campaignId)
            .Select(u => new UnitRow(
                u.Id,
                u.ArmyId,
                u.Army.SideId,
                u.Type,
                u.Points,
                u.UnsuppliedTurns
            ))
            .ToListAsync(cancellationToken);
        var depots = await db
            .Depots.AsNoTracking()
            .Where(d => d.Army.CampaignId == campaignId)
            .ToListAsync(cancellationToken);
        var supplyDepots = depots
            .Select(d => new SupplyDepot(d.Id, d.ArmyId, new Hex(d.Q, d.R), d.Kind, d.CutOffTurns))
            .ToList();
        var (nowPlaces, nextPlaces) = await Whereabouts.LoadAsync(
            db,
            campaignId,
            cancellationToken
        );
        static List<SupplyUnit> ForSupply(List<UnitPlace> places) =>
            [
                .. places.Select(p => new SupplyUnit(
                    p.UnitId,
                    p.ArmyId,
                    p.SideId,
                    p.At,
                    p.Type,
                    p.Points,
                    p.LivesOffTheLand
                )),
            ];
        var (now, next) = (ForSupply(nowPlaces), ForSupply(nextPlaces));
        var (nowSupply, connectedNow) = SupplyLines.Of(map, now, supplyDepots);
        var (nextSupply, connectedNext) = SupplyLines.Of(map, next, supplyDepots);
        return new(
            nowSupply,
            nextSupply,
            connectedNow,
            connectedNext,
            units.ToDictionary(u => u.Id, u => (u.ArmyId, u.UnsuppliedTurns)),
            depots
        );
    }

    private sealed record UnitRow(
        Guid Id,
        Guid ArmyId,
        Guid SideId,
        UnitType Type,
        int Points,
        int UnsuppliedTurns
    );

    /// <summary>The campaign's routes, its armies' sides, and its supply settings.</summary>
    private static async Task<SupplyMap> MapAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var campaign = await CalendarEndpoints.LoadAsync(db, campaignId, cancellationToken);
        var edges = await db
            .HexEdges.AsNoTracking()
            .Where(e => e.CampaignId == campaignId)
            .ToListAsync(cancellationToken);
        var armySides = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .ToDictionaryAsync(a => a.Id, a => a.SideId, cancellationToken);
        return new SupplyMap(
            SupplyLines.RoutesOf(edges),
            armySides,
            campaign.SupplyReach,
            [.. SupplySettingsEndpoints.ExemptTypes(campaign)]
        );
    }

    /// <summary>
    /// Counts the closing turn into each unit's and intermediate depot's supply (not saved): a
    /// turn more out of supply or cut off, or none.
    /// </summary>
    public static async Task CloseTurnAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var supply = await LoadAsync(db, campaignId, cancellationToken);
        var units = await db
            .ArmyUnits.Where(u => u.CampaignId == campaignId)
            .ToListAsync(cancellationToken);
        foreach (var unit in units)
        {
            unit.UnsuppliedTurns =
                supply.Next.GetValueOrDefault(unit.Id)?.State == SupplyState.Unsupplied
                    ? unit.UnsuppliedTurns + 1
                    : 0;
        }

        var depots = await db
            .Depots.Where(d => d.Army.CampaignId == campaignId && d.Kind == DepotKind.Intermediate)
            .ToListAsync(cancellationToken);
        foreach (var depot in depots)
        {
            depot.CutOffTurns = supply.ConnectedNext.GetValueOrDefault(depot.Id)
                ? 0
                : depot.CutOffTurns + 1;
        }
    }
}
