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
        app.MapPost("/api/armies/{id:guid}/units", CreateArmyUnitAsync)
            .WithName("CreateArmyUnit")
            .WithTags("ArmyUnits")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army);

        var unit = app.MapGroup("/api/army-units/{id:guid}").WithTags("ArmyUnits");
        unit.MapPut("", UpdateArmyUnitAsync)
            .WithName("UpdateArmyUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.ArmyUnit);
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
            .Select(u => new ArmyUnitResponse(
                u.Id,
                u.ArmyId,
                u.Name,
                u.Type,
                u.FightingFactor,
                u.Points
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(units);
    }

    /// <summary>Adds a unit to the army (Umpire or Admin).</summary>
    internal static async Task<Created<ArmyUnitResponse>> CreateArmyUnitAsync(
        Guid id,
        CreateArmyUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = new ArmyUnit
        {
            ArmyId = id,
            Name = request.Name,
            Type = request.Type,
            FightingFactor = request.FightingFactor,
            Points = request.Points,
        };
        db.ArmyUnits.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/army-units/{unit.Id}", ToResponse(unit));
    }

    /// <summary>Changes a unit's name, type, Fighting Factor and points (Umpire or Admin).</summary>
    internal static async Task<Ok<ArmyUnitResponse>> UpdateArmyUnitAsync(
        Guid id,
        UpdateArmyUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = await db.ArmyUnits.Where(u => u.Id == id).SingleOrGoneAsync(cancellationToken);
        unit.Name = request.Name;
        unit.Type = request.Type;
        unit.FightingFactor = request.FightingFactor;
        unit.Points = request.Points;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(unit));
    }

    /// <summary>Deletes a unit (Umpire or Admin).</summary>
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

    private static ArmyUnitResponse ToResponse(ArmyUnit unit) =>
        new(unit.Id, unit.ArmyId, unit.Name, unit.Type, unit.FightingFactor, unit.Points);
}
