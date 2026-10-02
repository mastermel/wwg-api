using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Join;

internal static class JoinEndpoints
{
    public static IEndpointRouteBuilder MapJoinEndpoints(this IEndpointRouteBuilder app)
    {
        var join = app.MapGroup("/api/join").WithTags("Join");

        // The code is 128 random bits, so the public preview can't be used to find campaigns.
        join.MapGet("/{code}", GetJoinPreviewAsync).WithName("GetJoinPreview").AllowAnonymous();
        join.MapPost("/{code}", JoinCampaignAsync).WithName("JoinCampaign").RequireSignedIn();

        return app;
    }

    /// <summary>The campaign a join link is for: its name and Umpire. No sign-in needed.</summary>
    internal static async Task<
        Results<Ok<JoinPreviewResponse>, ProblemHttpResult>
    > GetJoinPreviewAsync(string code, WwgDbContext db, CancellationToken cancellationToken)
    {
        var preview = await db
            .Campaigns.AsNoTracking()
            .Where(c => c.JoinCode == code)
            .Select(c => new JoinPreviewResponse(
                c.Name,
                c.Members.Where(m => m.Role == CampaignRole.Umpire)
                    .Select(m => m.User.FirstName + " " + m.User.LastName)
                    .FirstOrDefault()
            ))
            .SingleOrDefaultAsync(cancellationToken);
        return preview is null ? NoSuchLink() : TypedResults.Ok(preview);
    }

    /// <summary>
    /// Joins the campaign as a Player: 201, and its Umpires are emailed (decision 0023). Already a
    /// member (including its Umpire): 200 with the existing role, and nothing changes.
    /// </summary>
    internal static async Task<
        Results<Created<JoinCampaignResponse>, Ok<JoinCampaignResponse>, ProblemHttpResult>
    > JoinCampaignAsync(
        string code,
        ClaimsPrincipal principal,
        WwgDbContext db,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        CancellationToken cancellationToken
    )
    {
        var userId = principal.GetUserId();
        var campaign = await db
            .Campaigns.AsNoTracking()
            .Where(c => c.JoinCode == code)
            .Select(c => new
            {
                c.Id,
                MyRole = c
                    .Members.Where(m => m.UserId == userId)
                    .Select(m => (CampaignRole?)m.Role)
                    .FirstOrDefault(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (campaign is null)
        {
            return NoSuchLink();
        }

        if (campaign.MyRole is { } existing)
        {
            return TypedResults.Ok(new JoinCampaignResponse(campaign.Id, existing));
        }

        // Two joins racing past the check hit the unique (CampaignId, UserId) index: 409.
        db.CampaignMembers.Add(
            new CampaignMember
            {
                CampaignId = campaign.Id,
                UserId = userId,
                Role = CampaignRole.Player,
            }
        );
        await db.SaveChangesAsync(cancellationToken);
        await CampaignEmails.PlayerJoinedAsync(
            db,
            emails,
            appOptions,
            campaign.Id,
            userId,
            cancellationToken
        );
        return TypedResults.Created(
            $"/api/campaigns/{campaign.Id}",
            new JoinCampaignResponse(campaign.Id, CampaignRole.Player)
        );
    }

    private static ProblemHttpResult NoSuchLink() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status404NotFound,
            detail: "This join link doesn't work. It may have been replaced; ask your Umpire for a new one."
        );
}
