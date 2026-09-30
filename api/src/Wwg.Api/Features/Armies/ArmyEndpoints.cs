using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure;
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
            .RequireCampaignAccess(CampaignAccess.Member, CampaignRouteId.Army);
        army.MapPut("", UpdateArmyAsync)
            .WithName("UpdateArmy")
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
                    ),
                a.Side == null ? null : new ArmySide(a.Side.Id, a.Side.Name),
                a.Color,
                a.Nation
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(armies);
    }

    /// <summary>
    /// Adds an army, optionally with a commander and side (Umpire or Admin). A campaign has at
    /// most 8 armies (409). Without a colour it gets the first one no other army has.
    /// </summary>
    internal static async Task<
        Results<Created<ArmyResponse>, ValidationProblem, ProblemHttpResult>
    > CreateArmyAsync(
        Guid id,
        CreateArmyRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var colors = await db
            .Armies.Where(a => a.CampaignId == id)
            .Select(a => a.Color)
            .ToListAsync(cancellationToken);
        if (colors.Count >= Army.MaxPerCampaign)
        {
            return TooManyArmies();
        }

        if (await InvalidSideAsync(db, id, request.SideId, cancellationToken) is { } badSide)
        {
            return badSide;
        }

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
            SideId = request.SideId,
            Color = request.Color ?? FreeColor(colors),
            Nation = request.Nation ?? Nation.None,
        };
        db.Armies.Add(army);
        await TurnRules.JoinTurnsAsync(db, army, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created(
            $"/api/armies/{army.Id}",
            await LoadAsync(db, army.Id, cancellationToken)
        );
    }

    /// <summary>
    /// An army's details and units. Every member sees every army's (read-only); where the units
    /// are is a separate question (DESIGN.md §5.2, the visibility rule).
    /// </summary>
    internal static async Task<Ok<ArmyResponse>> GetArmyAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(await LoadAsync(db, id, cancellationToken));

    /// <summary>Changes an army's name, side, colour and nation (Umpire or Admin).</summary>
    internal static async Task<Results<Ok<ArmyResponse>, ValidationProblem>> UpdateArmyAsync(
        Guid id,
        UpdateArmyRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var army = await db.Armies.Where(a => a.Id == id).SingleOrGoneAsync(cancellationToken);
        if (
            await InvalidSideAsync(db, army.CampaignId, request.SideId, cancellationToken) is
            { } badSide
        )
        {
            return badSide;
        }

        army.Name = request.Name;
        army.SideId = request.SideId;
        army.Color = request.Color;
        army.Nation = request.Nation;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
    }

    /// <summary>A validation problem on <c>sideId</c> unless it's null or one of the campaign's.</summary>
    private static async Task<ValidationProblem?> InvalidSideAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid? sideId,
        CancellationToken cancellationToken
    ) =>
        sideId is not { } side
        || await db.Sides.AnyAsync(
            f => f.Id == side && f.CampaignId == campaignId,
            cancellationToken
        )
            ? null
            : TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["sideId"] = ["There's no such side in this campaign."],
                }
            );

    private static ProblemHttpResult TooManyArmies() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "Too many armies",
            detail: $"A campaign has at most {Army.MaxPerCampaign} armies."
        );

    /// <summary>The first colour no army has, or else the least used.</summary>
    private static ArmyColor FreeColor(List<ArmyColor> taken) =>
        Enum.GetValues<ArmyColor>().OrderBy(c => taken.Count(t => t == c)).First();

    /// <summary>
    /// Deletes an army and its units (Umpire or Admin), while the campaign is setting up; after it
    /// starts, that would erase their history (409).
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> DeleteArmyAsync(
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
            return TurnRules.CantDeleteAfterTheStart("army");
        }

        // While setting up, its only history is its turn 0 (and so its units' placements).
        await db.ArmyTurns.Where(t => t.ArmyId == id).ExecuteDeleteAsync(cancellationToken);
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
        var army = await db.Armies.Where(a => a.Id == id).SingleOrGoneAsync(cancellationToken);
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
        TimeProvider time,
        CancellationToken cancellationToken
    )
    {
        // ExecuteUpdate skips the audit interceptor, so UpdatedAt is set here.
        await db
            .Armies.Where(a => a.Id == id)
            .ExecuteUpdateAsync(
                s =>
                    s.SetProperty(a => a.CommanderId, (Guid?)null)
                        .SetProperty(a => a.UpdatedAt, time.GetUtcNow().UtcDateTime),
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
                a.Side == null ? null : new ArmySide(a.Side.Id, a.Side.Name),
                a.Color,
                a.Nation,
                db.ArmyUnits.Where(u => u.ArmyId == a.Id)
                    .OrderBy(u => u.Name)
                    .ThenBy(u => u.Id)
                    .Select(u => new ArmyUnitResponse(
                        u.Id,
                        u.ArmyId,
                        u.Name,
                        u.Type,
                        u.FightingFactor,
                        u.Points
                    ))
                    .ToList(),
                a.CreatedAt,
                a.UpdatedAt
            ))
            .SingleOrGoneAsync(cancellationToken);
}
