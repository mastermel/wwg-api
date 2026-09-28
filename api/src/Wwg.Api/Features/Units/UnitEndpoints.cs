using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
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
        unit.MapPut("", RenameUnitAsync)
            .WithName("RenameUnit")
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
        var unit = new Unit { ArmyId = id, Name = request.Name };
        db.Units.Add(unit);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/units/{unit.Id}",
            new UnitResponse(unit.Id, unit.ArmyId, unit.Name)
        );
    }

    /// <summary>Renames a unit (Umpire or Admin).</summary>
    internal static async Task<Ok<UnitResponse>> RenameUnitAsync(
        Guid id,
        RenameUnitRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var unit = await db.Units.SingleAsync(u => u.Id == id, cancellationToken);
        unit.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new UnitResponse(unit.Id, unit.ArmyId, unit.Name));
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
}
