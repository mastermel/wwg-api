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

    /// <summary>
    /// The army's commander, the campaign's Umpire, or an Admin. Army routes only: other members
    /// get 403.
    /// </summary>
    Commander,

    /// <summary>The campaign's Umpire, or an Admin.</summary>
    Umpire,

    /// <summary>
    /// The army's commander, and no one else: giving orders is theirs alone (DESIGN.md §5.2), the
    /// one rule Admins don't pass. Army and army-turn routes only.
    /// </summary>
    OwnCommander,
}

/// <summary>What the route's <c>{id}</c> is, and so how the campaign is found from it.</summary>
internal enum CampaignRouteId
{
    /// <summary>The campaign itself (<c>/api/campaigns/{id}/...</c>).</summary>
    Campaign,

    /// <summary>An army in the campaign (<c>/api/armies/{id}/...</c>).</summary>
    Army,

    /// <summary>A unit in one of the campaign's armies (<c>/api/units/{id}</c>).</summary>
    Unit,

    /// <summary>One of the campaign's factions (<c>/api/factions/{id}</c>).</summary>
    Faction,

    /// <summary>An army's turn (<c>/api/army-turns/{id}/...</c>): its army's campaign and commander.</summary>
    ArmyTurn,
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
    /// route's <c>{id}</c>: the campaign's, an army's, a unit's or a faction's
    /// (<paramref name="routeId"/>). The handler
    /// should still take <c>Guid id</c>, which documents it in the OpenAPI document (a path
    /// parameter nothing binds is left out, and the document is invalid).
    /// Not a member (or no such campaign, army or unit): 404, so outsiders can't tell it exists. A member
    /// without enough access: 403. Admins always pass.
    /// </summary>
    public static TBuilder RequireCampaignAccess<TBuilder>(
        this TBuilder builder,
        CampaignAccess access,
        CampaignRouteId routeId = CampaignRouteId.Campaign
    )
        where TBuilder : IEndpointConventionBuilder
    {
        // Only an army (or army-turn) route knows which army, so which commander.
        if (
            access is CampaignAccess.Commander or CampaignAccess.OwnCommander
            && routeId is not (CampaignRouteId.Army or CampaignRouteId.ArmyTurn)
        )
        {
            throw new ArgumentException("Commander access needs an army route.", nameof(access));
        }

        return builder
            .RequireAuthorization()
            .WithMetadata(new AccessRuleMetadata($"campaign:{access}"))
            .AddEndpointFilter(new CampaignAccessFilter(access, routeId));
    }

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

    private sealed class CampaignAccessFilter(CampaignAccess access, CampaignRouteId routeId)
        : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(
            EndpointFilterInvocationContext context,
            EndpointFilterDelegate next
        )
        {
            var httpContext = context.HttpContext;
            if (!Guid.TryParse(httpContext.GetRouteValue("id") as string, out var id))
            {
                return NotFound();
            }

            var db = httpContext.RequestServices.GetRequiredService<WwgDbContext>();
            if (
                await ResolveAsync(db, id, httpContext.RequestAborted)
                is not var (campaignId, commanderId)
            )
            {
                return NotFound();
            }

            var userId = httpContext.User.GetUserId();
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
            var isCommander = commanderId is not null && campaign.Member?.Id == commanderId;
            var allowed =
                access == CampaignAccess.OwnCommander
                    ? isCommander
                    : isAdmin
                        || role == CampaignRole.Umpire
                        || access == CampaignAccess.Member
                        || (access == CampaignAccess.Commander && isCommander);
            if (!allowed)
            {
                return Forbidden();
            }

            httpContext.Features.Set(
                new CampaignContext(campaignId, isAdmin, role, campaign.Member?.Id)
            );
            return await next(context);
        }

        /// <summary>
        /// The campaign the route's ID belongs to, and (for an army or unit) the army's commander.
        /// Null if there's no such army or unit.
        /// </summary>
        private async Task<(Guid CampaignId, Guid? CommanderId)?> ResolveAsync(
            WwgDbContext db,
            Guid id,
            CancellationToken cancellationToken
        )
        {
            if (routeId == CampaignRouteId.Campaign)
            {
                return (id, null);
            }

            if (routeId == CampaignRouteId.Faction)
            {
                var campaignId = await db
                    .Factions.AsNoTracking()
                    .Where(f => f.Id == id)
                    .Select(f => (Guid?)f.CampaignId)
                    .FirstOrDefaultAsync(cancellationToken);
                return campaignId is { } found ? (found, null) : null;
            }

            if (routeId == CampaignRouteId.ArmyTurn)
            {
                var turn = await db
                    .ArmyTurns.AsNoTracking()
                    .Where(t => t.Id == id)
                    .Select(t => new { t.Army.CampaignId, t.Army.CommanderId })
                    .FirstOrDefaultAsync(cancellationToken);
                return turn is null ? null : (turn.CampaignId, turn.CommanderId);
            }

            var army =
                routeId == CampaignRouteId.Army
                    ? await db
                        .Armies.AsNoTracking()
                        .Where(a => a.Id == id)
                        .Select(a => new { a.CampaignId, a.CommanderId })
                        .FirstOrDefaultAsync(cancellationToken)
                    : await db
                        .Units.AsNoTracking()
                        .Where(u => u.Id == id)
                        .Select(u => new { u.Army.CampaignId, u.Army.CommanderId })
                        .FirstOrDefaultAsync(cancellationToken);
            return army is null ? null : (army.CampaignId, army.CommanderId);
        }

        private ProblemHttpResult Forbidden() =>
            TypedResults.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: access switch
                {
                    CampaignAccess.Commander =>
                        "Only the army's commander or the campaign's Umpire can see this.",
                    CampaignAccess.OwnCommander => "Only the army's commander can give its orders.",
                    _ => "Only the campaign's Umpire can do this.",
                }
            );

        private static ProblemHttpResult NotFound() =>
            TypedResults.Problem(statusCode: StatusCodes.Status404NotFound);
    }
}
