using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Sightings;

/// <summary>Keeping the sightings the Umpire shaped as a turn starts (step 49b, decision 0020).</summary>
internal static class SightingRecords
{
    /// <summary>
    /// Records each sighting the Umpire confirmed for the turn starting (not saved), with what was
    /// seen copied in from where the closing turn left the units, or says why one won't do. A
    /// sighting the app didn't find is the Umpire's own (spies, scouting parties).
    /// </summary>
    public static async Task<string?> ApplyAsync(
        WwgDbContext db,
        Guid campaignId,
        int turn,
        IReadOnlyList<SightingRequest> confirmed,
        CancellationToken cancellationToken
    )
    {
        if (confirmed.Count == 0)
        {
            return null;
        }

        var (_, next) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        var found = (await SightingEndpoints.DueAsync(db, campaignId, cancellationToken))
            .Select(c => (c.ObservingArmyId, c.At))
            .ToHashSet();
        var sides = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == campaignId)
            .ToDictionaryAsync(a => a.Id, a => a.SideId, cancellationToken);
        foreach (
            var request in confirmed
                .GroupBy(r => (r.ObservingArmyId, r.Q, r.R))
                .Select(g => g.Last())
        )
        {
            if (!sides.TryGetValue(request.ObservingArmyId, out var side))
            {
                return "That army isn't in this campaign.";
            }

            var hex = new Hex(request.Q, request.R);
            var seen = next.Where(p => p.At == hex && p.SideId != side).ToList();
            if (seen.Count == 0)
            {
                return $"Hex ({request.Q}, {request.R}) holds none of the other side's units.";
            }
            if (request.Strength == SightingStrength.Rough && request.Size is null)
            {
                return "Give a rough size, or show the strength another way.";
            }

            db.Sightings.Add(Record(request, turn, hex, seen, next, found));
        }

        return null;
    }

    private static Sighting Record(
        SightingRequest request,
        int turn,
        Hex hex,
        List<UnitPlace> seen,
        List<UnitPlace> places,
        HashSet<(Guid, Hex)> found
    )
    {
        var observer = places
            .Where(p => p.ArmyId == request.ObservingArmyId)
            .MinBy(p => p.At.Distance(hex));
        return new Sighting
        {
            ObservingArmyId = request.ObservingArmyId,
            Turn = turn,
            Q = hex.Q,
            R = hex.R,
            ShowsHex = request.ShowsHex,
            Whereabouts = observer is null ? "Reported to the army" : Sight.Roughly(hex, observer),
            ArmyIds = request.ShowsArmies ? [.. seen.Select(p => p.ArmyId).Distinct()] : null,
            UnitTypes = request.ShowsTypes ? [.. seen.Select(p => p.Type).Order()] : null,
            Strength = request.Strength,
            Size = request.Strength == SightingStrength.Rough ? request.Size : null,
            Points = request.Strength == SightingStrength.Exact ? seen.Sum(p => p.Points) : null,
            ByUmpire = !found.Contains((request.ObservingArmyId, hex)),
        };
    }
}
