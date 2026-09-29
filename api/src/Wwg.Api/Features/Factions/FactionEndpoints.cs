using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Factions;

internal static class FactionEndpoints
{
    public static IEndpointRouteBuilder MapFactionEndpoints(this IEndpointRouteBuilder app)
    {
        // Shallow nesting (DESIGN.md §3.3): the list under its campaign, each faction on its own.
        var campaignFactions = app.MapGroup("/api/campaigns/{id:guid}/factions")
            .WithTags("Factions");
        campaignFactions
            .MapGet("", ListFactionsAsync)
            .WithName("ListFactions")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaignFactions
            .MapPost("", CreateFactionAsync)
            .WithName("CreateFaction")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var faction = app.MapGroup("/api/factions/{id:guid}").WithTags("Factions");
        faction
            .MapPut("", RenameFactionAsync)
            .WithName("RenameFaction")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Faction)
            .ProducesProblem(StatusCodes.Status409Conflict);
        faction
            .MapDelete("", DeleteFactionAsync)
            .WithName("DeleteFaction")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Faction);

        return app;
    }

    /// <summary>The campaign's factions, sorted by name, with how many armies each has.</summary>
    internal static async Task<Ok<List<FactionResponse>>> ListFactionsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var factions = await db
            .Factions.AsNoTracking()
            .Where(f => f.CampaignId == id)
            .OrderBy(f => f.Name)
            .ThenBy(f => f.Id)
            .Select(f => new FactionResponse(
                f.Id,
                f.Name,
                db.Armies.Count(a => a.FactionId == f.Id)
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(factions);
    }

    /// <summary>Adds a faction (Umpire or Admin). 409 if the campaign already has one by that name.</summary>
    internal static async Task<
        Results<Created<FactionResponse>, ProblemHttpResult>
    > CreateFactionAsync(
        Guid id,
        CreateFactionRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (await NameTakenAsync(db, id, request.Name, exceptId: null, cancellationToken))
        {
            return NameTaken(request.Name);
        }

        var faction = new Faction { CampaignId = id, Name = request.Name };
        db.Factions.Add(faction);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Created(
            $"/api/factions/{faction.Id}",
            new FactionResponse(faction.Id, faction.Name, 0)
        );
    }

    /// <summary>Renames a faction (Umpire or Admin). 409 if another has that name.</summary>
    internal static async Task<Results<Ok<FactionResponse>, ProblemHttpResult>> RenameFactionAsync(
        Guid id,
        RenameFactionRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var faction = await db.Factions.Where(f => f.Id == id).SingleOrGoneAsync(cancellationToken);
        if (await NameTakenAsync(db, faction.CampaignId, request.Name, id, cancellationToken))
        {
            return NameTaken(request.Name);
        }

        faction.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(
            new FactionResponse(
                faction.Id,
                faction.Name,
                await db.Armies.CountAsync(a => a.FactionId == id, cancellationToken)
            )
        );
    }

    /// <summary>
    /// Deletes a faction (Umpire or Admin). Its armies are kept, unassigned; the Umpire assigns
    /// them to another before the campaign starts.
    /// </summary>
    internal static async Task<NoContent> DeleteFactionAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Factions.Where(f => f.Id == id).ExecuteDeleteAsync(cancellationToken);
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
        db.Factions.AnyAsync(
            f => f.CampaignId == campaignId && f.Name == name && f.Id != exceptId,
            cancellationToken
        );

    private static ProblemHttpResult NameTaken(string name) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Faction name in use",
            detail: $"This campaign already has a faction called {name}."
        );
}
