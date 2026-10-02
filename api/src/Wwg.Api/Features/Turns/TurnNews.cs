using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Supply;

namespace Wwg.Api.Features.Turns;

/// <summary>An ally's report that arrived: whose, and what it says.</summary>
internal sealed record ReportNews(string From, string? Message, int Sightings, bool Positions);

/// <summary>
/// What's new for an army as a turn opens (step 52c, decision 0023), in sentences: its sightings,
/// its supply, its attrition, its boats built and its allies' reports.
/// </summary>
internal sealed record TurnNews(
    IReadOnlyList<string> Sightings,
    IReadOnlyList<string> Supply,
    IReadOnlyList<string> Attrition,
    IReadOnlyList<string> Boats,
    IReadOnlyList<ReportNews> Reports
)
{
    public static readonly TurnNews None = new([], [], [], [], []);

    public bool IsEmpty =>
        Sightings.Count + Supply.Count + Attrition.Count + Boats.Count + Reports.Count == 0;
}

internal static partial class TurnNewsBuilder
{
    /// <summary>
    /// Each army's news as turn <paramref name="opened"/> opens, from what the closing turn (the
    /// one before) recorded: saved, so after the turn's close.
    /// </summary>
    public static async Task<Dictionary<Guid, TurnNews>> ForAsync(
        WwgDbContext db,
        Guid campaignId,
        int opened,
        CancellationToken cancellationToken
    )
    {
        var closing = opened - 1;
        var armies = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .ToDictionaryAsync(a => a.Id, a => a.Name, cancellationToken);
        var sightings = (await SightingsAsync(db, armies, opened, cancellationToken)).ToLookup(
            s => s.ArmyId,
            s => s.Text
        );
        var supply = (await SupplyAsync(db, campaignId, cancellationToken)).ToLookup(
            s => s.ArmyId,
            s => s.Text
        );
        var attrition = (await AttritionAsync(db, campaignId, closing, cancellationToken)).ToLookup(
            s => s.ArmyId,
            s => s.Text
        );
        var boats = (await BoatsAsync(db, campaignId, closing, cancellationToken)).ToLookup(
            s => s.ArmyId,
            s => s.Text
        );
        var reports = (await ReportsAsync(db, armies, opened, cancellationToken)).ToLookup(
            r => r.ArmyId,
            r => r.Report
        );
        return armies.Keys.ToDictionary(
            id => id,
            id => new TurnNews(
                [.. sightings[id]],
                [.. supply[id]],
                [.. attrition[id]],
                [.. boats[id]],
                [.. reports[id]]
            )
        );
    }

    /// <summary>The army's own sightings for the turn, worded as the map's Sightings panel does.</summary>
    private static async Task<List<(Guid ArmyId, string Text)>> SightingsAsync(
        WwgDbContext db,
        Dictionary<Guid, string> armies,
        int turn,
        CancellationToken cancellationToken
    )
    {
        var ids = armies.Keys.ToList();
        var seen = await db
            .Sightings.AsNoTracking()
            .Where(s =>
                ids.Contains(s.ObservingArmyId) && s.Turn == turn && s.SharedByArmyId == null
            )
            .OrderBy(s => s.CreatedAt)
            .ThenBy(s => s.Id)
            .ToListAsync(cancellationToken);
        return [.. seen.Select(s => (s.ObservingArmyId, Describe(s, armies)))];
    }

    private static string Describe(Sighting s, Dictionary<Guid, string> armies)
    {
        var where = s.ShowsHex ? HexName(new Hex(s.Q, s.R)) : s.Whereabouts;
        var whose = s.ArmyIds is { } ids
            ? string.Join(" and ", ids.Select(id => armies.GetValueOrDefault(id) ?? "an army"))
            : "Enemy troops";
        var what = s.UnitTypes is { } types ? $": {DescribeTypes(types)}" : "";
        var strength = s switch
        {
            { Strength: SightingStrength.Exact, Points: { } points } => $", {Points(points)}",
            { Strength: SightingStrength.Rough, Size: { } size } =>
                $", a {size.ToString().ToLowerInvariant()} force",
            _ => "",
        };
        var afloat = s.Afloat == true ? ", on boats" : "";
        var source = s.ByUmpire ? " (reported)" : "";
        return $"{where}: {whose}{what}{strength}{afloat}{source}.";
    }

    /// <summary>Units out of supply (newly, or for how long) and depots cut off, by army.</summary>
    private static async Task<List<(Guid ArmyId, string Text)>> SupplyAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var units = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.CampaignId == campaignId && u.UnsuppliedTurns > 0)
            .OrderByDescending(u => u.UnsuppliedTurns == 1)
            .ThenBy(u => u.Name)
            .Select(u => new
            {
                u.ArmyId,
                u.Name,
                u.UnsuppliedTurns,
            })
            .ToListAsync(cancellationToken);
        var depots = await db
            .Depots.AsNoTracking()
            .Where(d => d.Army.CampaignId == campaignId && d.CutOffTurns > 0)
            .OrderBy(d => d.CutOffTurns)
            .Select(d => new
            {
                d.ArmyId,
                d.Name,
                d.Q,
                d.R,
                d.CutOffTurns,
            })
            .ToListAsync(cancellationToken);
        return
        [
            .. units.Select(u =>
                (
                    u.ArmyId,
                    u.UnsuppliedTurns == 1 ? $"{u.Name} is out of supply."
                    : u.UnsuppliedTurns > SupplyLines.GraceTurns
                        ? $"{u.Name}: {Ordinal(u.UnsuppliedTurns)} turn out of supply, losing points to attrition."
                    : $"{u.Name}: {Ordinal(u.UnsuppliedTurns)} turn out of supply."
                )
            ),
            .. depots.Select(d =>
                (
                    d.ArmyId,
                    $"{d.Name ?? $"The depot in {HexName(new Hex(d.Q, d.R))}"} is cut off"
                        + (d.CutOffTurns == 1 ? "." : $" ({Ordinal(d.CutOffTurns)} turn).")
                )
            ),
        ];
    }

    /// <summary>Points lost to attrition as the turn closed, by army.</summary>
    private static async Task<List<(Guid ArmyId, string Text)>> AttritionAsync(
        WwgDbContext db,
        Guid campaignId,
        int closing,
        CancellationToken cancellationToken
    ) =>
        [
            .. (
                await db
                    .PointsChanges.AsNoTracking()
                    .Where(p =>
                        p.ArmyUnit.CampaignId == campaignId
                        && p.Turn == closing
                        && p.Reason == PointsChangeReason.Attrition
                        && p.Change < 0
                    )
                    .OrderBy(p => p.ArmyUnit.Name)
                    .Select(p => new
                    {
                        p.ArmyUnit.ArmyId,
                        p.ArmyUnit.Name,
                        p.Change,
                        p.PointsAfter,
                        p.Note,
                    })
                    .ToListAsync(cancellationToken)
            ).Select(p =>
                (
                    p.ArmyId,
                    $"{p.Name} lost {Points(-p.Change)} to attrition"
                        + (p.Note is { Length: > 0 } why ? $" ({why.ToLowerInvariant()})" : "")
                        + $"; now {Points(p.PointsAfter)}."
                )
            ),
        ];

    /// <summary>Boats an army built as the turn closed: placed in it, never in a turn before.</summary>
    private static async Task<List<(Guid ArmyId, string Text)>> BoatsAsync(
        WwgDbContext db,
        Guid campaignId,
        int closing,
        CancellationToken cancellationToken
    ) =>
        [
            .. (
                await db
                    .UnitOrders.AsNoTracking()
                    .Where(o =>
                        o.ArmyUnit.CampaignId == campaignId
                        && o.ArmyUnit.Type == UnitType.Boat
                        && o.ArmyUnit.UnitId == null
                        && o.ArmyTurn.CampaignTurn.Number == closing
                        && !db.UnitOrders.Any(earlier =>
                            earlier.UnitId == o.UnitId
                            && earlier.ArmyTurn.CampaignTurn.Number < closing
                        )
                    )
                    .OrderBy(o => o.ArmyUnit.Name)
                    .Select(o => new
                    {
                        o.ArmyUnit.ArmyId,
                        o.ArmyUnit.Name,
                        o.Q,
                        o.R,
                    })
                    .ToListAsync(cancellationToken)
            ).Select(b => (b.ArmyId, $"{b.Name} was built in {HexName(new Hex(b.Q, b.R))}.")),
        ];

    /// <summary>Allies' reports that arrived for the turn, by the army they came to.</summary>
    private static async Task<List<(Guid ArmyId, ReportNews Report)>> ReportsAsync(
        WwgDbContext db,
        Dictionary<Guid, string> armies,
        int turn,
        CancellationToken cancellationToken
    )
    {
        var ids = armies.Keys.ToList();
        var arrived = await db
            .IntelReports.AsNoTracking()
            .Where(r =>
                ids.Contains(r.ToArmyId)
                && r.Status == CourierStatus.Arrived
                && r.ArrivedTurn == turn
            )
            .OrderBy(r => r.SentTurn)
            .ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);
        return
        [
            .. arrived.Select(r =>
                (
                    r.ToArmyId,
                    new ReportNews(
                        armies.GetValueOrDefault(r.FromArmyId) ?? "An ally",
                        r.Note,
                        r.SightingIds.Count,
                        r.Snapshot is not null
                    )
                )
            ),
        ];
    }

    /// <summary>"Hex (3, −2)", as the app names a hex.</summary>
    private static string HexName(Hex hex) =>
        FormattableString.Invariant($"Hex ({hex.Q}, {hex.R})").Replace('-', '−');

    private static string Points(int n) =>
        n == 1 ? "1 point" : string.Create(CultureInfo.InvariantCulture, $"{n} points");

    private static string Ordinal(int n)
    {
        var suffix = (n % 100) is 11 or 12 or 13
            ? "th"
            : (n % 10) switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th",
            };
        return string.Create(CultureInfo.InvariantCulture, $"{n}{suffix}");
    }

    /// <summary>"2 line infantry and 1 light cavalry", as the map's Sightings panel says it.</summary>
    private static string DescribeTypes(IEnumerable<UnitType> types)
    {
        var parts = types
            .GroupBy(t => t)
            .Select(g =>
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{g.Count()} {Words().Replace(g.Key.ToString(), " ").ToLowerInvariant()}"
                )
            )
            .ToList();
        return parts.Count < 2
            ? parts.FirstOrDefault() ?? ""
            : $"{string.Join(", ", parts[..^1])} and {parts[^1]}";
    }

    // Before each capital inside a type's name ("LineInfantry"), where a space goes.
    [GeneratedRegex("(?<=[a-z])(?=[A-Z])", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Words();
}
