using Wwg.Api.Data;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Turns;

/// <summary>
/// The visibility rule (DESIGN.md §5.2, decision 0010): whose positions and orders the caller may
/// see. Every read of them goes through it. Today the Umpire and Admins see every army's, and a
/// commander their own; intelligence sharing and scouting will add grants here.
/// </summary>
internal static class Visibility
{
    /// <summary>The IDs of the campaign's armies whose positions the caller may see.</summary>
    public static IQueryable<Guid> VisibleArmyIds(WwgDbContext db, CampaignContext access) =>
        access.CanManage
            ? db.Armies.Where(a => a.CampaignId == access.CampaignId).Select(a => a.Id)
            : db
                .Armies.Where(a =>
                    a.CampaignId == access.CampaignId
                    && a.CommanderId != null
                    && a.CommanderId == access.MemberId
                )
                .Select(a => a.Id);
}
