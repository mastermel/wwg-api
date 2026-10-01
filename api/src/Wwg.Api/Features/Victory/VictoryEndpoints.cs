using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Victory;

/// <summary>Towns and victory points (step 50, decision 0021).</summary>
internal static class VictoryEndpoints
{
    public static IEndpointRouteBuilder MapVictoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/campaigns/{id:guid}/holdings/{q:int}/{r:int}", SetHoldingAsync)
            .WithName("SetHolding")
            .WithTags("Victory")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        app.MapGet("/api/campaigns/{id:guid}/scoreboard", GetScoreboardAsync)
            .WithName("GetScoreboard")
            .WithTags("Victory")
            .RequireCampaignAccess(CampaignAccess.Member);
        return app;
    }

    /// <summary>
    /// Sets who holds the settlement in hex (q, r), an army or no one (Umpire or Admin): who starts
    /// with it, or a correction. 404 where there's no settlement worth anything.
    /// </summary>
    internal static async Task<
        Results<NoContent, ValidationProblem, ProblemHttpResult>
    > SetHoldingAsync(
        Guid id,
        int q,
        int r,
        SetHoldingRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var at = new Hex(q, r);
        if (!(await Holdings.SettlementsAsync(db, id, cancellationToken)).ContainsKey(at))
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "There's no town, city or fortress worth anything in that hex."
            );
        }
        if (
            request.ArmyId is { } armyId
            && !await db.Armies.AnyAsync(
                a => a.Id == armyId && a.CampaignId == id,
                cancellationToken
            )
        )
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["armyId"] = ["That army isn't in this campaign."],
                }
            );
        }

        var held = await db
            .Holdings.Where(h => h.CampaignId == id)
            .ToDictionaryAsync(h => new Hex(h.Q, h.R), cancellationToken);
        var open = await TurnRules.OpenTurnAsync(db, id, cancellationToken);
        Holdings.Hand(db, id, held, at, request.ArmyId, open?.Number ?? 0, byUmpire: true);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// The victory points (every member): every side's total, with its armies' parts, and after
    /// every turn; the settlements their own side holds, and the changes it made or suffered. The
    /// Umpire's and Admins', everything.
    /// </summary>
    internal static async Task<Ok<ScoreboardResponse>> GetScoreboardAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            (
                await Scoreboard.LoadAsync(db, id, httpContext.CampaignContext(), cancellationToken)
            ).Response()
        );
}
