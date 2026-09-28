using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Admin;

internal static class AdminCampaignEndpoints
{
    public static RouteGroupBuilder MapAdminCampaignEndpoints(this RouteGroupBuilder admin)
    {
        var campaigns = admin.MapGroup("/campaigns");

        campaigns
            .MapGet("", ListAllCampaignsAsync)
            .WithName("ListAllCampaigns")
            .ProducesValidationProblem();
        campaigns.MapPut("/{id:guid}/umpire", SetUmpireAsync).WithName("SetCampaignUmpire");

        return admin;
    }

    /// <summary>
    /// Every campaign, sorted by name, one page at a time. <c>search</c> matches any part of the
    /// name, ignoring case; <c>withoutUmpire</c> keeps only campaigns with no Umpire.
    /// </summary>
    internal static async Task<Ok<PagedResponse<AdminCampaignSummary>>> ListAllCampaignsAsync(
        WwgDbContext db,
        CancellationToken cancellationToken,
        [StringLength(100)] string? search = null,
        bool withoutUmpire = false,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize
    )
    {
        var query = db.Campaigns.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = Search.ContainsPattern(search.Trim());
            query = query.Where(c => EF.Functions.Like(c.Name, pattern, Search.EscapeCharacter));
        }

        if (withoutUmpire)
        {
            query = query.Where(c => !c.Members.Any(m => m.Role == CampaignRole.Umpire));
        }

        var result = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .Select(c => new AdminCampaignSummary(
                c.Id,
                c.Name,
                c.Members.Where(m => m.Role == CampaignRole.Umpire)
                    .Select(m => m.User.FirstName + " " + m.User.LastName)
                    .FirstOrDefault(),
                c.Members.Count(m => m.Role == CampaignRole.Player),
                c.CreatedAt
            ))
            .ToPagedAsync(page, pageSize, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>
    /// Makes a user the campaign's Umpire. A Player is promoted (and their army left unassigned);
    /// anyone else joins as the Umpire.
    /// The previous Umpire, if there is one, becomes a Player. Choosing the current Umpire changes
    /// nothing.
    /// </summary>
    internal static async Task<
        Results<Ok<CampaignResponse>, ValidationProblem, ProblemHttpResult>
    > SetUmpireAsync(
        Guid id,
        SetUmpireRequest request,
        ClaimsPrincipal principal,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (!await db.Campaigns.AnyAsync(c => c.Id == id, cancellationToken))
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, cancellationToken))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["userId"] = ["There's no user with this ID."],
                }
            );
        }

        await ReplaceUmpireAsync(db, id, request.UserId, cancellationToken);

        var callerId = principal.GetUserId();
        var myRole = await db
            .CampaignMembers.Where(m => m.CampaignId == id && m.UserId == callerId)
            .Select(m => (CampaignRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);
        return TypedResults.Ok(
            await CampaignEndpoints.LoadAsync(db, id, myRole, cancellationToken)
        );
    }

    private static async Task ReplaceUmpireAsync(
        WwgDbContext db,
        Guid id,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var members = await db
            .CampaignMembers.Where(m =>
                m.CampaignId == id && (m.Role == CampaignRole.Umpire || m.UserId == userId)
            )
            .ToListAsync(cancellationToken);
        var current = members.SingleOrDefault(m => m.Role == CampaignRole.Umpire);
        var chosen = members.SingleOrDefault(m => m.UserId == userId);

        if (current is null || current != chosen)
        {
            // SQLite checks the one-Umpire index row by row, so the old Umpire is demoted and
            // saved first. The transaction still makes the change all or nothing.
            await using var transaction = await db.Database.BeginTransactionAsync(
                cancellationToken
            );
            if (current is not null)
            {
                current.Role = CampaignRole.Player;
                await db.SaveChangesAsync(cancellationToken);
            }

            if (chosen is null)
            {
                db.CampaignMembers.Add(
                    new CampaignMember
                    {
                        CampaignId = id,
                        UserId = userId,
                        Role = CampaignRole.Umpire,
                    }
                );
            }
            else
            {
                // The Umpire can't command an army: a promoted Player's army is left unassigned.
                chosen.Role = CampaignRole.Umpire;
                await db
                    .Armies.Where(a => a.CommanderId == chosen.Id)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(a => a.CommanderId, (Guid?)null),
                        cancellationToken
                    );
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
    }
}
