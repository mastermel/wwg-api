using Microsoft.AspNetCore.Http.HttpResults;
using Wwg.Api.Data;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Turns;

/// <summary>Each unit's forced marches (step 47, decision 0018).</summary>
internal static class MarchEndpoints
{
    public static IEndpointRouteBuilder MapMarchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/armies/{id:guid}/marches", ListMarchesAsync)
            .WithName("ListMarches")
            .WithTags("Turns")
            .RequireCampaignAccess(CampaignAccess.Commander, CampaignRouteId.Army);
        return app;
    }

    /// <summary>
    /// Each of the army's units' forced marches as the open turn began, and the attrition moving
    /// this turn would cost, as a multiple of the rules' scale (its commander, the Umpire and
    /// Admins: they follow its moves).
    /// </summary>
    internal static async Task<Ok<List<UnitMarchResponse>>> ListMarchesAsync(
        Guid id,
        WwgDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var (before, open) = await Marches.LoadAsync(
            db,
            httpContext.CampaignContext().CampaignId,
            id,
            cancellationToken
        );
        return TypedResults.Ok(
            before
                .Select(entry =>
                {
                    var state = entry.Value;
                    var order = open.GetValueOrDefault(entry.Key);
                    return new UnitMarchResponse(
                        entry.Key,
                        state.MovesInRow,
                        state.ForceMarchesInRow,
                        state.ForcedMarchTurns,
                        state.After(moved: true, forceMarch: false).Multiplier,
                        state.After(moved: true, forceMarch: true).Multiplier,
                        order is null ? 0 : state.After(order.Moved, order.ForceMarch).Multiplier
                    );
                })
                .OrderBy(m => m.UnitId)
                .ToList()
        );
    }
}
