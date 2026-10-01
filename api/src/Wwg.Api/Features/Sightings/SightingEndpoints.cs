using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Sightings;

/// <summary>Sightings (step 49, decision 0020).</summary>
internal static class SightingEndpoints
{
    public static IEndpointRouteBuilder MapSightingEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/campaigns/{id:guid}/sightings/due", ListSightingsDueAsync)
            .WithName("ListSightingsDue")
            .WithTags("Sightings")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// The sightings the open turn's orders give (Umpire or Admin): for each army, each hex of the
    /// other side's units its units would see where the orders leave them, by the map's terrain.
    /// What starting the next turn asks the Umpire to shape.
    /// </summary>
    internal static async Task<Ok<List<SightingDueResponse>>> ListSightingsDueAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            (await DueAsync(db, id, cancellationToken))
                .Select(c => new SightingDueResponse(
                    c.ObservingArmyId,
                    c.At.Q,
                    c.At.R,
                    Sight.Roughly(c.At, c.Observer),
                    c.Screened,
                    [
                        .. c
                            .Units.OrderBy(u => u.Name, StringComparer.Ordinal)
                            .Select(u => new SightedUnitResponse(
                                u.UnitId,
                                u.ArmyId,
                                u.Name,
                                u.Type,
                                u.Points
                            )),
                    ]
                ))
                .ToList()
        );

    /// <summary>The sightings where the open turn's orders as given leave the units.</summary>
    public static async Task<List<SightCandidate>> DueAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var (_, next) = await Whereabouts.LoadAsync(db, campaignId, cancellationToken);
        var ground = await GroundAsync(db, campaignId, cancellationToken);
        return Sight.Candidates(next, hex => ground.GetValueOrDefault(hex, Terrain.Flat));
    }

    /// <summary>Each hex's ground on the map (Flat where nothing's stored).</summary>
    public static async Task<Dictionary<Hex, Terrain>> GroundAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        (
            await db
                .HexCells.AsNoTracking()
                .Where(c => c.CampaignId == campaignId)
                .Select(c => new
                {
                    c.Q,
                    c.R,
                    c.Terrain,
                })
                .ToListAsync(cancellationToken)
        ).ToDictionary(c => new Hex(c.Q, c.R), c => c.Terrain);
}
