using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Sides;

internal static class SideEndpoints
{
    public static IEndpointRouteBuilder MapSideEndpoints(this IEndpointRouteBuilder app)
    {
        // Shallow nesting (DESIGN.md §3.3): the list under its campaign, each side on its own.
        var campaignSides = app.MapGroup("/api/campaigns/{id:guid}/sides").WithTags("Sides");
        campaignSides
            .MapGet("", ListSidesAsync)
            .WithName("ListSides")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaignSides
            .MapPost("", CreateSideAsync)
            .WithName("CreateSide")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var side = app.MapGroup("/api/sides/{id:guid}").WithTags("Sides");
        side.MapPut("", RenameSideAsync)
            .WithName("RenameSide")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Side)
            .ProducesProblem(StatusCodes.Status409Conflict);
        side.MapDelete("", DeleteSideAsync)
            .WithName("DeleteSide")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Side);

        return app;
    }

    /// <summary>The campaign's sides, sorted by name, with how many armies each has.</summary>
    internal static async Task<Ok<List<SideResponse>>> ListSidesAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var sides = await db
            .Sides.AsNoTracking()
            .Where(f => f.CampaignId == id)
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .Select(f => new SideResponse(f.Id, f.Name, db.Armies.Count(a => a.SideId == f.Id)))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(sides);
    }

    /// <summary>Adds a side (Umpire or Admin). 409 if the campaign already has one by that name.</summary>
    internal static async Task<Results<Created<SideResponse>, ProblemHttpResult>> CreateSideAsync(
        Guid id,
        CreateSideRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await NameTakenAsync(db, id, request.Name, exceptId: null, cancellationToken))
        {
            return NameTaken(request.Name);
        }

        var side = new Side { CampaignId = id, Name = request.Name };
        db.Sides.Add(side);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/sides/{side.Id}",
            new SideResponse(side.Id, side.Name, 0)
        );
    }

    /// <summary>Renames a side (Umpire or Admin). 409 if another has that name.</summary>
    internal static async Task<Results<Ok<SideResponse>, ProblemHttpResult>> RenameSideAsync(
        Guid id,
        RenameSideRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var side = await db.Sides.Where(f => f.Id == id).SingleOrGoneAsync(cancellationToken);
        if (await NameTakenAsync(db, side.CampaignId, request.Name, id, cancellationToken))
        {
            return NameTaken(request.Name);
        }

        side.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(
            new SideResponse(
                side.Id,
                side.Name,
                await db.Armies.CountAsync(a => a.SideId == id, cancellationToken)
            )
        );
    }

    /// <summary>
    /// Deletes a side (Umpire or Admin). Its armies are kept, unassigned; the Umpire assigns
    /// them to another before the campaign starts.
    /// </summary>
    internal static async Task<NoContent> DeleteSideAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Sides.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    // Case-insensitive: the column is NOCASE (so is the unique index, the final guarantee).
    private static Task<bool> NameTakenAsync(
        WwgDbContext db,
        Guid campaignId,
        string name,
        Guid? exceptId,
        CancellationToken cancellationToken
    ) =>
        db.Sides.AnyAsync(
            f => f.CampaignId == campaignId && f.Name == name && f.Id != exceptId,
            cancellationToken
        );

    private static ProblemHttpResult NameTaken(string name) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Side name in use",
            detail: $"This campaign already has a side called {name}."
        );
}
