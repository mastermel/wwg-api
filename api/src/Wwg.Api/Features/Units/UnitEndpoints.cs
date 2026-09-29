using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Units;

internal static class UnitEndpoints
{
    public static IEndpointRouteBuilder MapUnitEndpoints(this IEndpointRouteBuilder app)
    {
        // Units are listed with their army (GET /api/armies/{id}), which only its commander, the
        // Umpire and Admins can see. Changing them is the Umpire's (or an Admin's) job.
        app.MapPost("/api/armies/{id:guid}/units", CreateUnitAsync)
            .WithName("CreateUnit")
            .WithTags("Units")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army);

        var unit = app.MapGroup("/api/units/{id:guid}").WithTags("Units");
        unit.MapPut("", UpdateUnitAsync)
            .WithName("UpdateUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Unit);
        unit.MapDelete("", DeleteUnitAsync)
            .WithName("DeleteUnit")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Unit);

        return app;
    }

    /// <summary>Adds a unit to the army (Umpire or Admin).</summary>
    internal static async Task<Created<UnitResponse>> CreateUnitAsync(
        Guid id,
        CreateUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = new Unit
        {
            ArmyId = id,
            Name = request.Name,
            Type = request.Type,
            FightingFactor = request.FightingFactor,
            Points = request.Points,
        };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/units/{unit.Id}", ToResponse(unit));
    }

    /// <summary>Changes a unit's name, type, Fighting Factor and points (Umpire or Admin).</summary>
    internal static async Task<Ok<UnitResponse>> UpdateUnitAsync(
        Guid id,
        UpdateUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = await db.Units.Where(u => u.Id == id).SingleOrGoneAsync(cancellationToken);
        unit.Name = request.Name;
        unit.Type = request.Type;
        unit.FightingFactor = request.FightingFactor;
        unit.Points = request.Points;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(unit));
    }

    /// <summary>Deletes a unit (Umpire or Admin).</summary>
    internal static async Task<NoContent> DeleteUnitAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Units.Where(u => u.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static UnitResponse ToResponse(Unit unit) =>
        new(unit.Id, unit.ArmyId, unit.Name, unit.Type, unit.FightingFactor, unit.Points);
}
