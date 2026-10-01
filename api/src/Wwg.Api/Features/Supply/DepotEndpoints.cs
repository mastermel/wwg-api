using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Supply;

/// <summary>Each army's depots (step 48a, decision 0019).</summary>
internal static class DepotEndpoints
{
    public static IEndpointRouteBuilder MapDepotEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/campaigns/{id:guid}/depots", ListDepotsAsync)
            .WithName("ListDepots")
            .WithTags("Supply")
            .RequireCampaignAccess(CampaignAccess.Member);
        app.MapPost("/api/armies/{id:guid}/depots", CreateDepotAsync)
            .WithName("CreateDepot")
            .WithTags("Supply")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army)
            .ProducesProblem(StatusCodes.Status409Conflict);
        var depot = app.MapGroup("/api/depots/{id:guid}").WithTags("Supply");
        depot
            .MapPut("", UpdateDepotAsync)
            .WithName("UpdateDepot")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Depot);
        depot
            .MapDelete("", DeleteDepotAsync)
            .WithName("DeleteDepot")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Depot);
        return app;
    }

    /// <summary>
    /// The campaign's depots the viewer may see, by army then name: the Umpire's and Admins', all;
    /// a commander's, their own army's; anyone else's, none (they follow its moves).
    /// </summary>
    internal static async Task<Ok<List<DepotResponse>>> ListDepotsAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var context = httpContext.CampaignContext();
        var grid = await CampaignMaps.GridAsync(db, id, cancellationToken);
        if (grid is null)
        {
            return TypedResults.Ok(new List<DepotResponse>());
        }

        var depots = await db
            .Depots.AsNoTracking()
            .Where(d =>
                d.Army.CampaignId == id
                && (context.CanManage || d.Army.CommanderId == context.MemberId)
            )
            .OrderBy(d => d.Army.Name)
            .ThenBy(d => d.Name)
            .ThenBy(d => d.Id)
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(depots.Select(d => ToResponse(grid, d)).ToList());
    }

    /// <summary>Places a depot for the army (Umpire or Admin), in a hex of the campaign's grid.</summary>
    internal static async Task<
        Results<Created<DepotResponse>, ValidationProblem, ProblemHttpResult>
    > CreateDepotAsync(
        Guid id,
        SaveDepotRequest request,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var grid = await CampaignMaps.GridAsync(
            db,
            httpContext.CampaignContext().CampaignId,
            cancellationToken
        );
        if (grid is null)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "No map yet",
                detail: "Choose the campaign's area in the map settings first."
            );
        }

        if (!grid.Contains(new Hex(request.Q, request.R)))
        {
            return Outside();
        }

        var depot = db.Depots.Add(new Depot { ArmyId = id }).Entity;
        Apply(depot, request);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created($"/api/depots/{depot.Id}", ToResponse(grid, depot));
    }

    /// <summary>Moves, renames or changes a depot (Umpire or Admin).</summary>
    internal static async Task<Results<Ok<DepotResponse>, ValidationProblem>> UpdateDepotAsync(
        Guid id,
        SaveDepotRequest request,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        // A depot's campaign has an area: it was placed in it.
        var grid = (
            await CampaignMaps.GridAsync(
                db,
                httpContext.CampaignContext().CampaignId,
                cancellationToken
            )
        )!;
        if (!grid.Contains(new Hex(request.Q, request.R)))
        {
            return Outside();
        }

        var depot = await db.Depots.Where(d => d.Id == id).SingleOrGoneAsync(cancellationToken);
        Apply(depot, request);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(grid, depot));
    }

    /// <summary>Removes a depot (Umpire or Admin): it was captured, destroyed or given up.</summary>
    internal static async Task<NoContent> DeleteDepotAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Depots.Where(d => d.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static void Apply(Depot depot, SaveDepotRequest request)
    {
        depot.Kind = request.Kind;
        depot.Name = string.IsNullOrEmpty(request.Name) ? null : request.Name;
        (depot.Q, depot.R) = (request.Q, request.R);
    }

    private static ValidationProblem Outside() =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["q"] = ["That's outside the campaign's area."],
            }
        );

    private static DepotResponse ToResponse(HexGrid grid, Depot depot)
    {
        var (latitude, longitude) = grid.Centre(new Hex(depot.Q, depot.R));
        return new(
            depot.Id,
            depot.ArmyId,
            depot.Kind,
            depot.Name,
            depot.Q,
            depot.R,
            latitude,
            longitude,
            depot.CutOffTurns
        );
    }
}
