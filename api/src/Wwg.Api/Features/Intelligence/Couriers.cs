using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Intelligence;

/// <summary>A unit in a report's snapshot, as stored.</summary>
internal sealed record SnapshotUnit(string Name, UnitType Type, int Q, int R, int Points);

/// <summary>Sending reports, and their couriers' rides as each turn closes (step 49c, decision 0020).</summary>
internal static class Couriers
{
    /// <summary>Within this many turns' ride, a report arrives as the next turn starts (§L: "two moves").</summary>
    public const double CloseByTurns = 2;

    /// <summary>
    /// The sender's unit and the recipient's nearest each other: where a courier sets out from and
    /// rides to. Null if either army has no units on the map.
    /// </summary>
    public static (Hex From, Hex To)? Ends(
        IReadOnlyList<UnitPlace> places,
        Guid fromArmyId,
        Guid toArmyId,
        Hex? courier = null
    )
    {
        var to = places.Where(p => p.ArmyId == toArmyId).ToList();
        if (to.Count == 0)
        {
            return null;
        }
        if (courier is { } at)
        {
            // `to` has units: checked just above.
            return (at, to.MinBy(p => p.At.Distance(at))!.At);
        }

        var from = places.Where(p => p.ArmyId == fromArmyId).ToList();
        return from.Count == 0
            ? null
            : from.SelectMany(f => to.Select(t => (From: f.At, To: t.At)))
                .MinBy(pair => pair.From.Distance(pair.To));
    }

    /// <summary>A report as it sets out, with what it carries copied in (not added).</summary>
    public static IntelReport NewReport(
        Guid fromArmyId,
        SendReportRequest request,
        int turn,
        IReadOnlyList<UnitPlace> now,
        List<Guid> sightingIds,
        Hex from,
        double? turnsToRide
    ) =>
        new()
        {
            FromArmyId = fromArmyId,
            ToArmyId = request.ToArmyId,
            SentTurn = turn,
            Note = string.IsNullOrEmpty(request.Note) ? null : request.Note,
            Snapshot = request.IncludesSnapshot
                ? SnapshotOf(now.Where(p => p.ArmyId == fromArmyId))
                : null,
            SightingIds = sightingIds,
            Status = CourierStatus.EnRoute,
            CourierQ = from.Q,
            CourierR = from.R,
            ArrivesNext = turnsToRide is { } turns && turns <= CloseByTurns,
        };

    /// <summary>The snapshot a report carries: the army's units where they are now.</summary>
    public static string SnapshotOf(IEnumerable<UnitPlace> units) =>
        JsonSerializer.Serialize(
            units
                .OrderBy(u => u.Name, StringComparer.Ordinal)
                .Select(u => new SnapshotUnit(u.Name, u.Type, u.At.Q, u.At.R, u.Points))
                .ToList()
        );

    public static List<SnapshotUnit> ReadSnapshot(string snapshot) =>
        JsonSerializer.Deserialize<List<SnapshotUnit>>(snapshot) ?? [];

    /// <summary>
    /// Rides every courier on its way a turn on, towards the recipient's unit nearest it where
    /// the closing turn leaves the units (not saved); one that gets next to it (or was close by
    /// when sent) arrives for the turn starting, and the sightings it carries become the
    /// recipient's.
    /// </summary>
    public static async Task RideAsync(
        WwgDbContext db,
        Guid campaignId,
        int starting,
        CancellationToken cancellationToken
    )
    {
        var reports = await db
            .IntelReports.Where(r =>
                r.FromArmy.CampaignId == campaignId && r.Status == CourierStatus.EnRoute
            )
            .ToListAsync(cancellationToken);
        if (reports.Count == 0)
        {
            return;
        }

        var grid = await CampaignMaps.GridAsync(db, campaignId, cancellationToken);
        var (_, next) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        foreach (var report in reports)
        {
            var courier = new Hex(report.CourierQ, report.CourierR);
            if (
                !report.ArrivesNext
                && grid is not null
                && Ends(next, report.FromArmyId, report.ToArmyId, courier) is var (_, to)
            )
            {
                var rides = await CourierRides.LoadAsync(
                    db,
                    grid,
                    campaignId,
                    [courier, to],
                    cancellationToken
                );
                courier = rides.Ride(courier, to);
                (report.CourierQ, report.CourierR) = (courier.Q, courier.R);
                report.ArrivesNext = courier.Distance(to) <= 1;
                if (!report.ArrivesNext)
                {
                    continue;
                }
            }

            if (report.ArrivesNext)
            {
                await ArriveAsync(db, report, starting, cancellationToken);
            }
        }
    }

    /// <summary>Delivers a report (not saved): the recipient has it, and its sightings, from this turn.</summary>
    private static async Task ArriveAsync(
        WwgDbContext db,
        IntelReport report,
        int turn,
        CancellationToken cancellationToken
    )
    {
        report.Status = CourierStatus.Arrived;
        report.ArrivedTurn = turn;
        var sightings = await db
            .Sightings.AsNoTracking()
            .Where(s => report.SightingIds.Contains(s.Id))
            .ToListAsync(cancellationToken);
        db.Sightings.AddRange(
            sightings.Select(s => new Sighting
            {
                ObservingArmyId = report.ToArmyId,
                Turn = s.Turn,
                Q = s.Q,
                R = s.R,
                ShowsHex = s.ShowsHex,
                Whereabouts = s.Whereabouts,
                ArmyIds = s.ArmyIds,
                UnitTypes = s.UnitTypes,
                Strength = s.Strength,
                Size = s.Size,
                Points = s.Points,
                ByUmpire = s.ByUmpire,
                SharedByArmyId = s.SharedByArmyId ?? report.FromArmyId,
            })
        );
    }
}
