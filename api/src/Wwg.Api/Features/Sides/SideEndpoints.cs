using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Sides;

internal static class SideEndpoints
{
    public static IEndpointRouteBuilder MapSideEndpoints(this IEndpointRouteBuilder app)
    {
        // Shallow nesting (DESIGN.md §3.3): the list under its campaign, each side on its own.
        // A campaign has exactly two (decision 0017), made with it: renamed, never added or deleted.
        var campaignSides = app.MapGroup("/api/campaigns/{id:guid}/sides").WithTags("Sides");
        campaignSides
            .MapGet("", ListSidesAsync)
            .WithName("ListSides")
            .RequireCampaignAccess(CampaignAccess.Member);

        var side = app.MapGroup("/api/sides/{id:guid}").WithTags("Sides");
        side.MapPut("", RenameSideAsync)
            .WithName("RenameSide")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Side)
            .ProducesProblem(StatusCodes.Status409Conflict);

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
