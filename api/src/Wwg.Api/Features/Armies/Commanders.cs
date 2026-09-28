using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Armies;

/// <summary>The rules for who can command an army (DESIGN.md §5.1).</summary>
internal static class Commanders
{
    /// <summary>
    /// A validation problem on <paramref name="field"/> unless the member is a Player in the
    /// campaign (the Umpire can't command an army); null if they are.
    /// </summary>
    public static async Task<ValidationProblem?> ValidateAsync(
        WwgDbContext db,
        Guid campaignId,
        Guid memberId,
        string field,
        CancellationToken cancellationToken
    )
    {
        var role = await db
            .CampaignMembers.Where(m => m.Id == memberId && m.CampaignId == campaignId)
            .Select(m => (CampaignRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);
        return role == CampaignRole.Player
            ? null
            : TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    [field] =
                    [
                        role is null
                            ? "That member isn't in this campaign."
                            : "The Umpire can't command an army.",
                    ],
                }
            );
    }

    /// <summary>
    /// 409 if the member already commands an army other than <paramref name="armyId"/>: a Player
    /// commands one at most. Null otherwise. (The unique index on the commander is the final
    /// guarantee if two requests race.)
    /// </summary>
    public static async Task<ProblemHttpResult?> ConflictAsync(
        WwgDbContext db,
        Guid memberId,
        Guid? armyId,
        CancellationToken cancellationToken
    )
    {
        var commanded = await db
            .Armies.Where(a => a.CommanderId == memberId && a.Id != armyId)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(cancellationToken);
        return commanded is null
            ? null
            : TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Already a commander",
                detail: $"That Player already commands {commanded}. A Player commands one army at most."
            );
    }
}
