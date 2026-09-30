using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Maps;

/// <summary>Hexes' actual terrain, found by the Umpire's dice on request (decision 0016).</summary>
internal static class HexDetailEndpoints
{
    public static IEndpointRouteBuilder MapHexDetailEndpoints(this IEndpointRouteBuilder app)
    {
        var details = app.MapGroup("/api/campaigns/{id:guid}/grid/details").WithTags("Maps");
        details
            .MapGet("", ListHexDetailsAsync)
            .WithName("ListHexDetails")
            .RequireCampaignAccess(CampaignAccess.Member);
        details
            .MapPost("/{q:int}/{r:int}/roll", RollHexDetailAsync)
            .WithName("RollHexDetail")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        details
            .MapPut("/{q:int}/{r:int}", UpdateHexDetailAsync)
            .WithName("UpdateHexDetail")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        details
            .MapDelete("/{q:int}/{r:int}", DeleteHexDetailAsync)
            .WithName("DeleteHexDetail")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }

    /// <summary>
    /// The hexes' actual terrain the caller may see: the Umpire (or an Admin) all of it, with who
    /// asked and who it's shown to; a member what's shown to their army, or to all.
    /// </summary>
    internal static async Task<Ok<List<HexDetailResponse>>> ListHexDetailsAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var context = httpContext.CampaignContext();
        var query = db.HexDetails.AsNoTracking().Where(d => d.CampaignId == id);
        if (!context.CanManage)
        {
            var armyIds = await db
                .Armies.Where(a => a.CampaignId == id && a.CommanderId == context.MemberId)
                .Select(a => a.Id)
                .ToListAsync(cancellationToken);
            query = query.Where(d =>
                d.ShownToAll
                || db.HexDetailReveals.Any(r => r.HexDetailId == d.Id && armyIds.Contains(r.ArmyId))
            );
        }

        var details = await query.OrderBy(d => d.Q).ThenBy(d => d.R).ToListAsync(cancellationToken);
        var ids = details.Select(d => d.Id).ToList();
        var reveals = context.CanManage
            ? await db
                .HexDetailReveals.AsNoTracking()
                .Where(r => ids.Contains(r.HexDetailId))
                .Select(r => new { r.HexDetailId, r.ArmyId })
                .ToListAsync(cancellationToken)
            : [];
        return TypedResults.Ok(
            details
                .Select(d =>
                    ToResponse(
                        d,
                        context.CanManage,
                        [.. reveals.Where(r => r.HexDetailId == d.Id).Select(r => r.ArmyId)]
                    )
                )
                .ToList()
        );
    }

    /// <summary>
    /// The Umpire shakes the rule book's three dice for a hex (Umpire or Admin; p. 57): the red
    /// die with its modifier from the hex's map terrain, the white, and the green if favourability
    /// is wanted. A new roll replaces the hex's old one, keeping who it's shown to.
    /// </summary>
    internal static async Task<
        Results<Ok<HexDetailResponse>, ValidationProblem, ProblemHttpResult>
    > RollHexDetailAsync(
        Guid id,
        int q,
        int r,
        RollHexDetailRequest request,
        WwgDbContext db,
        IDice dice,
        CancellationToken cancellationToken
    )
    {
        if (await HexProblemAsync(db, id, q, r, cancellationToken) is { } problem)
        {
            return problem;
        }

        var cell = await db
            .HexCells.AsNoTracking()
            .SingleOrDefaultAsync(
                c => c.CampaignId == id && c.Q == q && c.R == r,
                cancellationToken
            );
        var modifier = DetailTable.Modifier(cell?.Terrain ?? Terrain.Flat, cell?.Forest ?? false);
        var errors = await ArmyErrorsAsync(db, id, request.ForArmyId, [], cancellationToken);
        if (request.FlatMinusOne && modifier > 0)
        {
            errors["flatMinusOne"] = ["Only a flat hex can take one off the red die."];
        }

        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var roll = DetailTable.Roll(dice, modifier, request.FlatMinusOne, request.Favorability);
        var detail = await FindOrAddAsync(db, id, q, r, cancellationToken);
        (
            detail.Relief,
            detail.Scrub,
            detail.Village,
            detail.Woods,
            detail.Forest,
            detail.Farms,
            detail.Fields,
            detail.Streams,
            detail.Dominant,
            detail.Favorability
        ) = (
            roll.Relief,
            roll.Scrub,
            roll.Village,
            roll.Woods,
            roll.Forest,
            roll.Farms,
            roll.Fields,
            roll.Streams,
            roll.Dominant,
            roll.Favorability
        );
        (detail.RedDie, detail.WhiteDie, detail.GreenDie, detail.ForArmyId) = (
            roll.RedDie,
            roll.WhiteDie,
            roll.GreenDie,
            request.ForArmyId
        );
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, detail, cancellationToken));
    }

    /// <summary>
    /// The Umpire sets a hex's actual terrain and who sees it (Umpire or Admin): changing a roll,
    /// or setting one without dice. The dice, if any, are kept as they fell.
    /// </summary>
    internal static async Task<
        Results<Ok<HexDetailResponse>, ValidationProblem, ProblemHttpResult>
    > UpdateHexDetailAsync(
        Guid id,
        int q,
        int r,
        UpdateHexDetailRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await HexProblemAsync(db, id, q, r, cancellationToken) is { } problem)
        {
            return problem;
        }

        var shownTo = request.ShownToArmyIds.Distinct().ToList();
        var errors = await ArmyErrorsAsync(db, id, request.ForArmyId, shownTo, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.ValidationProblem(errors);
        }

        var detail = await FindOrAddAsync(db, id, q, r, cancellationToken);
        var f = request.Features;
        (
            detail.Relief,
            detail.Scrub,
            detail.Village,
            detail.Woods,
            detail.Forest,
            detail.Farms,
            detail.Fields,
            detail.Streams
        ) = (request.Relief, f.Scrub, f.Village, f.Woods, f.Forest, f.Farms, f.Fields, f.Streams);
        (detail.Dominant, detail.Favorability, detail.ForArmyId, detail.ShownToAll) = (
            request.Dominant,
            request.Favorability,
            request.ForArmyId,
            request.ShownToAll
        );

        var reveals = await db
            .HexDetailReveals.Where(v => v.HexDetailId == detail.Id)
            .ToListAsync(cancellationToken);
        db.HexDetailReveals.RemoveRange(reveals.Where(v => !shownTo.Contains(v.ArmyId)));
        db.HexDetailReveals.AddRange(
            shownTo
                .Where(armyId => !reveals.Exists(v => v.ArmyId == armyId))
                .Select(armyId => new HexDetailReveal { HexDetailId = detail.Id, ArmyId = armyId })
        );
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, detail, cancellationToken));
    }

    /// <summary>Forgets a hex's actual terrain (Umpire or Admin), and who it was shown to.</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> DeleteHexDetailAsync(
        Guid id,
        int q,
        int r,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        await db
            .HexDetails.Where(d => d.CampaignId == id && d.Q == q && d.R == r)
            .ExecuteDeleteAsync(cancellationToken) == 0
            ? GridEndpoints.NoSuch("hex detail")
            : TypedResults.NoContent();

    /// <summary>409 without an area; 404 for a hex outside the grid.</summary>
    private static async Task<ProblemHttpResult?> HexProblemAsync(
        WwgDbContext db,
        Guid campaignId,
        int q,
        int r,
        CancellationToken cancellationToken
    ) =>
        await CampaignMaps.GridAsync(db, campaignId, cancellationToken) is not { } grid
            ? GridEndpoints.NoArea()
        : !grid.Contains(new Hex(q, r)) ? GridEndpoints.NoSuch("hex")
        : null;

    /// <summary>The asking army and those shown it, each one of the campaign's.</summary>
    private static async Task<Dictionary<string, string[]>> ArmyErrorsAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid? forArmyId,
        List<Guid> shownTo,
        CancellationToken cancellationToken
    )
    {
        var wanted = shownTo.Concat(forArmyId is { } asking ? [asking] : []).Distinct().ToList();
        var known = await db
            .Armies.Where(a => a.CampaignId == campaignId && wanted.Contains(a.Id))
            .Select(a => a.Id)
            .ToListAsync(cancellationToken);
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (forArmyId is { } army && !known.Contains(army))
        {
            errors["forArmyId"] = ["There's no such army in this campaign."];
        }

        if (shownTo.Exists(a => !known.Contains(a)))
        {
            errors["shownToArmyIds"] = ["There's no such army in this campaign."];
        }

        return errors;
    }

    private static async Task<HexDetail> FindOrAddAsync(
        WwgDbContext db,
        Guid campaignId,
        int q,
        int r,
        CancellationToken cancellationToken
    )
    {
        var detail = await db.HexDetails.SingleOrDefaultAsync(
            d => d.CampaignId == campaignId && d.Q == q && d.R == r,
            cancellationToken
        );
        if (detail is null)
        {
            detail = new HexDetail
            {
                CampaignId = campaignId,
                Q = q,
                R = r,
            };
            db.HexDetails.Add(detail);
        }

        return detail;
    }

    private static async Task<HexDetailResponse> LoadAsync(
        WwgDbContext db,
        HexDetail detail,
        CancellationToken cancellationToken
    ) =>
        ToResponse(
            detail,
            true,
            await db
                .HexDetailReveals.Where(v => v.HexDetailId == detail.Id)
                .Select(v => v.ArmyId)
                .ToListAsync(cancellationToken)
        );

    /// <summary>A detail as the caller sees it: who asked and who sees it are the Umpire's.</summary>
    private static HexDetailResponse ToResponse(HexDetail d, bool umpire, List<Guid> shownTo) =>
        new(
            d.Q,
            d.R,
            d.Relief,
            new HexFeatures(d.Scrub, d.Village, d.Woods, d.Forest, d.Farms, d.Fields, d.Streams),
            d.Dominant,
            d.Favorability,
            d is { RedDie: { } red, WhiteDie: { } white }
                ? new HexDice(red, white, d.GreenDie)
                : null,
            umpire ? d.ForArmyId : null,
            umpire ? shownTo : [],
            d.ShownToAll
        );
}
