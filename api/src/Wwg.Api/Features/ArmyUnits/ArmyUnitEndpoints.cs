using System.Linq.Expressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.ArmyUnits;

internal static class ArmyUnitEndpoints
{
    public static IEndpointRouteBuilder MapArmyUnitEndpoints(this IEndpointRouteBuilder app)
    {
        // Every member sees every unit: with its army (GET /api/armies/{id}), or all the
        // campaign's at once (for the map). Changing them is the Umpire's (or an Admin's) job.
        app.MapGet("/api/campaigns/{id:guid}/units", ListCampaignUnitsAsync)
            .WithName("ListCampaignUnits")
            .WithTags("ArmyUnits")
            .RequireCampaignAccess(CampaignAccess.Member);
        // A campaign only chooses from the library (decision 0015).
        app.MapPost("/api/armies/{id:guid}/units", AddArmyUnitsAsync)
            .WithName("AddArmyUnits")
            .WithTags("ArmyUnits")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var unit = app.MapGroup("/api/army-units/{id:guid}").WithTags("ArmyUnits");
        unit.MapPut("", UpdateArmyUnitAsync)
            .WithName("UpdateArmyUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyUnit);
        unit.MapGet("/points", ListPointsHistoryAsync)
            .WithName("ListPointsHistory")
            .RequireCampaignAccess(CampaignAccess.Member, CampaignRouteId.ArmyUnit);
        unit.MapDelete("", DeleteArmyUnitAsync)
            .WithName("DeleteArmyUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyUnit);

        return app;
    }

    /// <summary>
    /// Every unit in the campaign, sorted by army then name (every member): what the map draws
    /// where it may (positions follow the visibility rule).
    /// </summary>
    internal static async Task<Ok<List<ArmyUnitResponse>>> ListCampaignUnitsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var units = await db
            .ArmyUnits.AsNoTracking()
            .Where(u => u.Army.CampaignId == id)
            .OrderBy(u => u.Army.Name)
            .ThenBy(u => u.Name)
            .ThenBy(u => u.Id)
            .Select(ArmyUnitProjection.ToResponse)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(units);
    }

    /// <summary>
    /// Adds library units to the army (Umpire or Admin), each as the campaign's own copy. Only
    /// units from the army's factions (409), and none already in the campaign (409).
    /// </summary>
    internal static async Task<
        Results<Ok<List<ArmyUnitResponse>>, ValidationProblem, ProblemHttpResult>
    > AddArmyUnitsAsync(
        Guid id,
        AddArmyUnitsRequest request,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var campaignId = httpContext.CampaignContext().CampaignId;
        var ids = request.UnitIds.Distinct().ToList();
        var units = await db
            .Units.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .ToListAsync(cancellationToken);
        if (units.Count != ids.Count)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["unitIds"] = ["There's no such unit in the library."],
                }
            );
        }

        if (await RefusedAsync(db, id, campaignId, units, cancellationToken) is { } refused)
        {
            return refused;
        }

        var added = units
            .Select(u => new ArmyUnit
            {
                ArmyId = id,
                CampaignId = campaignId,
                UnitId = u.Id,
                Name = u.Name,
                Type = u.Type,
                FightingFactor = u.FightingFactor,
                Points = u.Points,
            })
            .ToList();
        db.ArmyUnits.AddRange(added);
        await db.SaveChangesAsync(cancellationToken);

        var addedIds = added.Select(u => u.Id).ToList();
        return TypedResults.Ok(
            await db
                .ArmyUnits.AsNoTracking()
                .Where(u => addedIds.Contains(u.Id))
                .OrderBy(u => u.Name)
                .ThenBy(u => u.Id)
                .Select(ArmyUnitProjection.ToResponse)
                .ToListAsync(cancellationToken)
        );
    }

    /// <summary>
    /// 409 unless every unit is from the army's factions, and none is in the campaign already.
    /// </summary>
    private static async Task<ProblemHttpResult?> RefusedAsync(
        WwgDbContext db,
        Guid armyId,
        Guid campaignId,
        List<Unit> units,
        CancellationToken cancellationToken
    )
    {
        var factions = await db
            .ArmyFactions.Where(f => f.ArmyId == armyId)
            .Select(f => f.FactionId)
            .ToListAsync(cancellationToken);
        var outside = units
            .Where(u => !factions.Contains(u.FactionId))
            .Select(u => u.Name)
            .ToList();
        if (outside.Count > 0)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Not from the army's factions",
                detail: $"Choose the army's factions first: {string.Join(", ", outside)}."
            );
        }

        var ids = units.Select(u => u.Id).ToList();
        var taken = await db
            .ArmyUnits.Where(u => u.CampaignId == campaignId && ids.Contains(u.UnitId))
            .Select(u => u.Name)
            .ToListAsync(cancellationToken);
        return taken.Count == 0
            ? null
            : TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Already in the campaign",
                detail: $"Already in this campaign: {string.Join(", ", taken)}."
            );
    }

    /// <summary>
    /// Changes the campaign's copy of a unit: name, type, Fighting Factor and points (Umpire or
    /// Admin). The library unit stays as it is.
    /// </summary>
    internal static async Task<Ok<ArmyUnitResponse>> UpdateArmyUnitAsync(
        Guid id,
        UpdateArmyUnitRequest request,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var unit = await db.ArmyUnits.Where(u => u.Id == id).SingleOrGoneAsync(cancellationToken);
        // Once the campaign has started, a change to its points goes in its history (decision 0018).
        var open = await TurnRules.OpenTurnAsync(db, unit.CampaignId, cancellationToken);
        if (open is { Number: > 0 } && request.Points != unit.Points)
        {
            db.PointsChanges.Add(
                new PointsChange
                {
                    ArmyUnitId = id,
                    Turn = open.Number,
                    Change = request.Points - unit.Points,
                    PointsAfter = request.Points,
                    Reason = PointsChangeReason.Edited,
                    ByUserId = httpContext.User.GetUserId(),
                }
            );
        }

        unit.Name = request.Name;
        unit.Type = request.Type;
        unit.FightingFactor = request.FightingFactor;
        unit.Points = request.Points;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(
            await db
                .ArmyUnits.AsNoTracking()
                .Where(u => u.Id == id)
                .Select(ArmyUnitProjection.ToResponse)
                .SingleAsync(cancellationToken)
        );
    }

    /// <summary>
    /// The unit's points history once the campaign started, oldest first (every member, as they
    /// see its points): each change, its turn, and why.
    /// </summary>
    internal static async Task<Ok<List<PointsChangeResponse>>> ListPointsHistoryAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            await db
                .PointsChanges.AsNoTracking()
                .Where(c => c.ArmyUnitId == id)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Select(c => new PointsChangeResponse(
                    c.Turn,
                    c.Change,
                    c.PointsAfter,
                    c.Reason,
                    c.Note,
                    c.ByUser == null ? null : c.ByUser.FirstName + " " + c.ByUser.LastName,
                    c.CreatedAt
                ))
                .ToListAsync(cancellationToken)
        );

    /// <summary>
    /// Removes a unit from the army (Umpire or Admin), while the campaign is setting up; the
    /// library unit stays.
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> DeleteArmyUnitAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        if (
            await TurnRules.HasStartedAsync(
                db,
                httpContext.CampaignContext().CampaignId,
                cancellationToken
            )
        )
        {
            return TurnRules.CantDeleteAfterTheStart("unit");
        }

        // While setting up, its only history is its placement.
        await db.UnitOrders.Where(o => o.UnitId == id).ExecuteDeleteAsync(cancellationToken);
        await db.ArmyUnits.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }
}

/// <summary>An army unit as the API gives it, in queries (EF Core turns it into SQL).</summary>
internal static class ArmyUnitProjection
{
    public static readonly Expression<Func<ArmyUnit, ArmyUnitResponse>> ToResponse =
        u => new ArmyUnitResponse(
            u.Id,
            u.ArmyId,
            u.UnitId,
            u.Unit.FactionId,
            // It marches as its faction's nation (step 45), or its army's if the faction has none.
            u.Unit.Faction.Nation != Nation.None
                ? u.Unit.Faction.Nation
                : u.Army.Nation,
            u.Name,
            u.Type,
            u.FightingFactor,
            u.Points
        );
}
