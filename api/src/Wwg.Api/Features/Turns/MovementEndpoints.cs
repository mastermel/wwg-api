using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Turns;

/// <summary>A campaign's movement table (step 44): the rule book's, or the Umpire's.</summary>
internal static class MovementEndpoints
{
    public static IEndpointRouteBuilder MapMovementEndpoints(this IEndpointRouteBuilder app)
    {
        var movement = app.MapGroup("/api/campaigns/{id:guid}/movement").WithTags("Turns");
        movement
            .MapGet("", GetMovementTableAsync)
            .WithName("GetMovementTable")
            .RequireCampaignAccess(CampaignAccess.Member);
        movement
            .MapPut("", SaveMovementTableAsync)
            .WithName("SaveMovementTable")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        movement
            .MapDelete("", ResetMovementTableAsync)
            .WithName("ResetMovementTable")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// The campaign's movement table (every member): hexes a turn for each class on each ground.
    /// The rule book's until the Umpire changes it.
    /// </summary>
    internal static async Task<Ok<MovementTableResponse>> GetMovementTableAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(await LoadAsync(db, id, cancellationToken));

    /// <summary>
    /// Replaces the campaign's movement table (Umpire or Admin): every class on each of its
    /// grounds (land units on land, boats on water), once, 0 to 20 hexes a turn in halves. It
    /// applies to orders given from now on.
    /// </summary>
    internal static async Task<
        Results<Ok<MovementTableResponse>, ValidationProblem>
    > SaveMovementTableAsync(
        Guid id,
        SaveMovementTableRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var cells = request.Rates.Select(r => (r.Class, r.Ground)).ToList();
        var problem =
            cells.Count != MovementRates.Cells
            || !cells.ToHashSet().SetEquals(MovementTable.Rules.Keys)
                ? "Give every class on each of its grounds, once."
            : request.Rates.Any(r => r.Hexes * 2 != Math.Floor(r.Hexes * 2))
                ? "Give hexes a turn in halves (0.5, 1, 1.5…)."
            : null;
        if (problem is not null)
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal) { ["rates"] = [problem] }
            );
        }

        await db.MovementRates.Where(m => m.CampaignId == id).ExecuteDeleteAsync(cancellationToken);
        // Only where it differs from the rules: a table like theirs is theirs.
        db.MovementRates.AddRange(
            request
                .Rates.Where(r =>
                    MovementTable.Rules[(r.Class, r.Ground)] is var rule && rule != r.Hexes
                )
                .Select(r => new MovementRate
                {
                    CampaignId = id,
                    Class = r.Class,
                    Ground = r.Ground,
                    Hexes = r.Hexes,
                })
        );
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
    }

    /// <summary>Goes back to the rule book's movement table (Umpire or Admin).</summary>
    internal static async Task<NoContent> ResetMovementTableAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        await db.MovementRates.Where(m => m.CampaignId == id).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<MovementTableResponse> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var table = await MovementTable.LoadAsync(db, campaignId, cancellationToken);
        var rates = MovementTable
            .Rules.Keys.Select(key => new MovementRateDto(
                key.Item1,
                key.Item2,
                table.Rate(key.Item1, key.Item2)
            ))
            .ToList();
        return new MovementTableResponse(
            rates,
            rates.TrueForAll(r => MovementTable.Rules[(r.Class, r.Ground)] == r.Hexes)
        );
    }
}
