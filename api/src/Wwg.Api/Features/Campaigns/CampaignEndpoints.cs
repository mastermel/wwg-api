using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Campaigns;

internal static class CampaignEndpoints
{
    public static IEndpointRouteBuilder MapCampaignEndpoints(this IEndpointRouteBuilder app)
    {
        var campaigns = app.MapGroup("/api/campaigns").WithTags("Campaigns");

        campaigns
            .MapGet("", ListMyCampaignsAsync)
            .WithName("ListMyCampaigns")
            .RequireSignedIn()
            .ProducesValidationProblem();
        campaigns.MapPost("", CreateCampaignAsync).WithName("CreateCampaign").RequireSignedIn();
        campaigns
            .MapGet("/{id:guid}", GetCampaignAsync)
            .WithName("GetCampaign")
            .RequireCampaignAccess(CampaignAccess.Member);
        campaigns
            .MapPut("/{id:guid}", UpdateCampaignAsync)
            .WithName("UpdateCampaign")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        campaigns
            .MapDelete("/{id:guid}", DeleteCampaignAsync)
            .WithName("DeleteCampaign")
            .RequireCampaignAccess(CampaignAccess.Umpire);

        return app;
    }

    /// <summary>The campaigns the caller is in, with their role, sorted by name.</summary>
    internal static async Task<Ok<PagedResponse<CampaignSummary>>> ListMyCampaignsAsync(
        ClaimsPrincipal principal,
        WwgDbContext db,
        CancellationToken cancellationToken,
        [Range(1, int.MaxValue)] int page = 1,
        [Range(1, Paging.MaxPageSize)] int pageSize = Paging.DefaultPageSize
    )
    {
        var userId = principal.GetUserId();
        var result = await db
            .CampaignMembers.AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderBy(m => m.Campaign.Name)
            .ThenBy(m => m.CampaignId)
            .Select(m => new CampaignSummary(
                m.CampaignId,
                m.Campaign.Name,
                m.Role,
                m.Campaign.Members.Where(u => u.Role == CampaignRole.Umpire)
                    .Select(u => u.User.FirstName + " " + u.User.LastName)
                    .FirstOrDefault(),
                m.Campaign.Members.Count(p => p.Role == CampaignRole.Player)
            ))
            .ToPagedAsync(page, pageSize, cancellationToken);
        return TypedResults.Ok(result);
    }

    /// <summary>Starts a campaign. The caller becomes its Umpire.</summary>
    internal static async Task<Created<CampaignResponse>> CreateCampaignAsync(
        CreateCampaignRequest request,
        ClaimsPrincipal principal,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var campaign = new Campaign
        {
            Name = request.Name,
            Description = EmptyToNull(request.Description),
            JoinCode = JoinCodes.Generate(),
        };
        campaign.Members.Add(
            new CampaignMember { UserId = principal.GetUserId(), Role = CampaignRole.Umpire }
        );
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync(cancellationToken);

        var response = await LoadAsync(db, campaign.Id, CampaignRole.Umpire, cancellationToken);
        return TypedResults.Created($"/api/campaigns/{campaign.Id}", response);
    }

    /// <summary>A campaign's details: its Umpire (if it has one) and the caller's role.</summary>
    internal static async Task<Ok<CampaignResponse>> GetCampaignAsync(
        HttpContext httpContext,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var access = httpContext.CampaignContext();
        return TypedResults.Ok(
            await LoadAsync(db, access.CampaignId, access.Role, cancellationToken)
        );
    }

    /// <summary>Changes the campaign's name and description (Umpire or Admin).</summary>
    internal static async Task<Ok<CampaignResponse>> UpdateCampaignAsync(
        UpdateCampaignRequest request,
        HttpContext httpContext,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var access = httpContext.CampaignContext();
        var campaign = await db.Campaigns.SingleAsync(
            c => c.Id == access.CampaignId,
            cancellationToken
        );
        campaign.Name = request.Name;
        campaign.Description = EmptyToNull(request.Description);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Ok(await LoadAsync(db, campaign.Id, access.Role, cancellationToken));
    }

    /// <summary>
    /// Deletes the campaign and everything in it: members now, armies and units later (Umpire or
    /// Admin). The database's cascades do the rest.
    /// </summary>
    internal static async Task<NoContent> DeleteCampaignAsync(
        HttpContext httpContext,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var campaignId = httpContext.CampaignContext().CampaignId;
        await db.Campaigns.Where(c => c.Id == campaignId).ExecuteDeleteAsync(cancellationToken);
        return TypedResults.NoContent();
    }

    internal static string? EmptyToNull(string? value) =>
        string.IsNullOrEmpty(value) ? null : value;

    internal static Task<CampaignResponse> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CampaignRole? myRole,
        CancellationToken cancellationToken
    ) =>
        db
            .Campaigns.AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => new CampaignResponse(
                c.Id,
                c.Name,
                c.Description,
                c.Members.Where(m => m.Role == CampaignRole.Umpire)
                    .Select(m => new CampaignUmpire(
                        m.Id,
                        m.UserId,
                        m.User.FirstName,
                        m.User.LastName
                    ))
                    .FirstOrDefault(),
                myRole,
                c.Members.Count(m => m.Role == CampaignRole.Player),
                c.CreatedAt,
                c.UpdatedAt
            ))
            .SingleAsync(cancellationToken);
}
