using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Campaigns;

internal static class MemberEndpoints
{
    public static IEndpointRouteBuilder MapMemberEndpoints(this IEndpointRouteBuilder app)
    {
        var campaign = app.MapGroup("/api/campaigns/{id:guid}").WithTags("Campaigns");

        campaign
            .MapGet("/join-code", GetJoinCodeAsync)
            .WithName("GetJoinCode")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        campaign
            .MapPost("/join-code", RegenerateJoinCodeAsync)
            .WithName("RegenerateJoinCode")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        campaign
            .MapGet("/members", ListMembersAsync)
            .WithName("ListCampaignMembers")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaign
            .MapDelete("/members/me", LeaveCampaignAsync)
            .WithName("LeaveCampaign")
            .RequireCampaignAccess(CampaignAccess.Member)
            .ProducesProblem(StatusCodes.Status409Conflict);
        campaign
            .MapDelete("/members/{memberId:guid}", RemoveMemberAsync)
            .WithName("RemoveCampaignMember")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    /// <summary>The campaign's current join code (Umpire or Admin).</summary>
    internal static async Task<Ok<JoinCodeResponse>> GetJoinCodeAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var joinCode = await db
            .Campaigns.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => c.JoinCode)
            .SingleOrGoneAsync(cancellationToken);
        return TypedResults.Ok(new JoinCodeResponse(joinCode));
    }

    /// <summary>
    /// Replaces the join code, so the old join link stops working (Umpire or Admin). Existing
    /// members stay.
    /// </summary>
    internal static async Task<Ok<JoinCodeResponse>> RegenerateJoinCodeAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        campaign.JoinCode = JoinCodes.Generate();
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new JoinCodeResponse(campaign.JoinCode));
    }

    /// <summary>
    /// The campaign's members, with the army each Player commands: the Umpire first, then Players
    /// by name.
    /// </summary>
    internal static async Task<Ok<List<CampaignMemberResponse>>> ListMembersAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var members = await db
            .CampaignMembers.AsNoTracking()
            .Where(m => m.CampaignId == id)
            .OrderBy(m => m.Role == CampaignRole.Umpire ? 0 : 1)
            .ThenBy(m => m.User.LastName)
            .ThenBy(m => m.User.FirstName)
            .ThenBy(m => m.Id)
            .Select(m => new CampaignMemberResponse(
                m.Id,
                m.UserId,
                m.User.FirstName,
                m.User.LastName,
                m.Role,
                m.CreatedAt,
                db.Armies.Where(a => a.CommanderId == m.Id)
                    .Select(a => new MemberArmy(a.Id, a.Name))
                    .FirstOrDefault()
            ))
            .ToListAsync(cancellationToken);
        return TypedResults.Ok(members);
    }

    /// <summary>
    /// Leaves the campaign (Players). The Umpire can't leave: an Admin sets a new Umpire instead.
    /// </summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> LeaveCampaignAsync(
        Guid id,
        HttpContext httpContext,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var access = httpContext.CampaignContext();
        if (access.MemberId is not { } memberId)
        {
            // An Admin who isn't a member has nothing to leave.
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (access.Role == CampaignRole.Umpire)
        {
            return UmpireStays("The Umpire can't leave the campaign.");
        }

        await db.CampaignMembers.Where(m => m.Id == memberId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>Removes a Player from the campaign (Umpire or Admin).</summary>
    internal static async Task<Results<NoContent, ProblemHttpResult>> RemoveMemberAsync(
        Guid id,
        Guid memberId,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var role = await db
            .CampaignMembers.Where(m => m.Id == memberId && m.CampaignId == id)
            .Select(m => (CampaignRole?)m.Role)
            .SingleOrDefaultAsync(cancellationToken);
        if (role is null)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
        }

        if (role == CampaignRole.Umpire)
        {
            return UmpireStays("The Umpire can't be removed.");
        }

        await db.CampaignMembers.Where(m => m.Id == memberId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    private static ProblemHttpResult UmpireStays(string detail) =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The Umpire stays",
            detail: $"{detail} An Admin can set a new Umpire."
        );
}
