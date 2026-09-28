using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure.Auth;

/// <summary>The minimum access a campaign endpoint needs.</summary>
internal enum CampaignAccess
{
    /// <summary>Any member (Umpire or Player), or an Admin.</summary>
    Member,

    /// <summary>The campaign's Umpire, or an Admin.</summary>
    Umpire,
}

/// <summary>
/// The caller's relationship to the campaign a request is about, resolved once by
/// <see cref="CampaignAccessExtensions.RequireCampaignAccess"/> before the handler runs.
/// </summary>
internal sealed record CampaignContext(
    Guid CampaignId,
    bool IsAdmin,
    CampaignRole? Role,
    Guid? MemberId
)
{
    /// <summary>The Umpire, or an Admin: can change the campaign and everything in it.</summary>
    public bool CanManage => IsAdmin || Role == CampaignRole.Umpire;
}

internal static class CampaignAccessExtensions
{
    /// <summary>
    /// Declares a campaign endpoint's access rule (DESIGN.md §3.5). The campaign comes from the
    /// route's <c>{id}</c>; the handler should still take <c>Guid id</c>, which documents it in the
    /// OpenAPI document (a path parameter nothing binds is left out, and the document is invalid). Not a member (or no such campaign): 404, so outsiders can't tell it
    /// exists. A member without enough access: 403. Admins always pass.
    /// </summary>
    public static TBuilder RequireCampaignAccess<TBuilder>(
        this TBuilder builder,
        CampaignAccess access
    )
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .RequireAuthorization()
            .WithMetadata(new AccessRuleMetadata($"campaign:{access}"))
            .AddEndpointFilter(new CampaignAccessFilter(access));

    /// <summary>The <see cref="CampaignContext"/> the access filter resolved for this request.</summary>
    public static CampaignContext CampaignContext(this HttpContext httpContext) =>
        httpContext.Features.Get<CampaignContext>()
        ?? throw new InvalidOperationException(
            "No CampaignContext: the endpoint needs .RequireCampaignAccess(...)."
        );

    /// <summary>The signed-in user's ID.</summary>
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.Parse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("Not signed in.")
        );

    private sealed class CampaignAccessFilter(CampaignAccess access) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next
        )
        {
            var httpContext = context.HttpContext;
            if (!Guid.TryParse(httpContext.GetRouteValue("id") as string, out var campaignId))
            {
                return NotFound();
            }

            var userId = httpContext.User.GetUserId();
            var db = httpContext.RequestServices.GetRequiredService<WwgDbContext>();
            var campaign = await db
                .Campaigns.AsNoTracking()
                .Where(c => c.Id == campaignId)
                .Select(c => new
                {
                    Member = c
                        .Members.Where(m => m.UserId == userId)
                        .Select(m => new { m.Id, m.Role })
                        .FirstOrDefault(),
                })
                .FirstOrDefaultAsync(httpContext.RequestAborted);

            var isAdmin = httpContext.User.IsInRole(Roles.Admin);
            if (campaign is null || (campaign.Member is null && !isAdmin))
            {
                return NotFound();
            }

            var role = campaign.Member?.Role;
            if (!isAdmin && access == CampaignAccess.Umpire && role != CampaignRole.Umpire)
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status403Forbidden,
                    detail: "Only the campaign's Umpire can do this."
                );
            }

            httpContext.Features.Set(
                new CampaignContext(campaignId, isAdmin, role, campaign.Member?.Id)
            );
            return await next(context);
        }

        private static ProblemHttpResult NotFound() =>
            TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}
