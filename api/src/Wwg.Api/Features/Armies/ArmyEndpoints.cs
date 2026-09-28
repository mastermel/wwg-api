using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Armies;

internal static class ArmyEndpoints
{
    public static IEndpointRouteBuilder MapArmyEndpoints(this IEndpointRouteBuilder app)
    {
        // Shallow nesting (DESIGN.md §3.3): the list under its campaign, each army on its own.
        var campaignArmies = app.MapGroup("/api/campaigns/{id:guid}/armies").WithTags("Armies");
        campaignArmies
            .MapGet("", ListArmiesAsync)
            .WithName("ListArmies")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaignArmies
            .MapPost("", CreateArmyAsync)
            .WithName("CreateArmy")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var army = app.MapGroup("/api/armies/{id:guid}").WithTags("Armies");
        army.MapGet("", GetArmyAsync)
            .WithName("GetArmy")
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.Army);
        army.MapPut("", RenameArmyAsync)
            .WithName("RenameArmy")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army);
        army.MapDelete("", DeleteArmyAsync)
            .WithName("DeleteArmy")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army);
        army.MapPut("/commander", AssignCommanderAsync)
            .WithName("AssignCommander")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army)
            .ProducesProblem(StatusCodes.Status409Conflict);
        army.MapDelete("/commander", UnassignCommanderAsync)
            .WithName("UnassignCommander")
            .RequireCampaignAccess(CampaignAccess.Umpire, CampaignRouteId.Army);

        return app;
    }

    /// <summary>
    /// Every army in the campaign, with its commander, sorted by name. All members see them all,
    /// unassigned ones included.
    /// </summary>
    internal static async Task<Ok<List<ArmySummary>>> ListArmiesAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var armies = await db
            .Armies.AsNoTracking()
            .Where(a => a.CampaignId == id)
            .OrderBy(a => a.Name)
            .ThenBy(a => a.Id)
            .Select(a => new ArmySummary(
                a.Id,
                a.Name,
                a.Commander == null
                    ? null
                    : new ArmyCommander(
                        a.Commander.Id,
                        a.Commander.UserId,
                        a.Commander.User.FirstName,
                        a.Commander.User.LastName
                    )
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(armies);
    }

    /// <summary>Adds an army, optionally with a commander (Umpire or Admin).</summary>
    internal static async Task<
        Results<Created<ArmyResponse>, ValidationProblem, ProblemHttpResult>
    > CreateArmyAsync(
        Guid id,
        CreateArmyRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (request.CommanderMemberId is { } memberId)
        {
            var invalid = await Commanders.ValidateAsync(
                db,
                id,
                memberId,
                "commanderMemberId",
                cancellationToken
            );
            if (invalid is not null)
            {
                return invalid;
            }

            if (
                await Commanders.ConflictAsync(db, memberId, null, cancellationToken) is
                { } conflict
            )
            {
                return conflict;
            }
        }

        var army = new Army
        {
            CampaignId = id,
            Name = request.Name,
            CommanderId = request.CommanderMemberId,
        };
        db.Armies.Add(army);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/armies/{army.Id}",
            await LoadAsync(db, army.Id, cancellationToken)
        );
    }

    /// <summary>
    /// An army's details (its commander, the campaign's Umpire, or an Admin; other Players get
    /// 403).
    /// </summary>
    internal static async Task<Ok<ArmyResponse>> GetArmyAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(await LoadAsync(db, id, cancellationToken));

    /// <summary>Renames an army (Umpire or Admin).</summary>
    internal static async Task<Ok<ArmyResponse>> RenameArmyAsync(
        Guid id,
        RenameArmyRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var army = await db.Armies.SingleAsync(a => a.Id == id, cancellationToken);
        army.Name = request.Name;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
    }

    /// <summary>Deletes an army (Umpire or Admin). The database deletes its units too.</summary>
    internal static async Task<NoContent> DeleteArmyAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.Armies.Where(a => a.Id == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Makes a Player the army's commander, replacing any other (Umpire or Admin). A Player
    /// commands at most one army: 409 if they already command another.
    /// </summary>
    internal static async Task<
        Results<Ok<ArmyResponse>, ValidationProblem, ProblemHttpResult>
    > AssignCommanderAsync(
        Guid id,
        AssignCommanderRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var army = await db.Armies.SingleAsync(a => a.Id == id, cancellationToken);
        var invalid = await Commanders.ValidateAsync(
            db,
            army.CampaignId,
            request.MemberId,
            "memberId",
            cancellationToken
        );
        if (invalid is not null)
        {
            return invalid;
        }

        if (
            await Commanders.ConflictAsync(db, request.MemberId, id, cancellationToken) is
            { } conflict
        )
        {
            return conflict;
        }

        army.CommanderId = request.MemberId;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
    }

    /// <summary>Leaves the army without a commander (Umpire or Admin).</summary>
    internal static async Task<NoContent> UnassignCommanderAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db
            .Armies.Where(a => a.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.CommanderId, (Guid?)null),
                cancellationToken
            );
        return TypedResults.NoContent();
    }

    private static Task<ArmyResponse> LoadAsync(
        WwgDbContext db,
        Guid armyId,
        CancellationToken cancellationToken
    ) =>
        db
            .Armies.AsNoTracking()
            .Where(a => a.Id == armyId)
            .Select(a => new ArmyResponse(
                a.Id,
                a.CampaignId,
                a.Campaign.Name,
                a.Name,
                a.Commander == null
                    ? null
                    : new ArmyCommander(
                        a.Commander.Id,
                        a.Commander.UserId,
                        a.Commander.User.FirstName,
                        a.Commander.User.LastName
                    ),
                a.CreatedAt,
                a.UpdatedAt
            ))
            .SingleAsync(cancellationToken);
}
