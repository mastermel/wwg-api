using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Intelligence;
using Wwg.Api.Features.Sightings;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Campaigns;

/// <summary>
/// The campaign's other emails (step 52d, decision 0023): a player given an army, and the Umpires
/// told when a player joins and when every army has submitted the open turn.
/// </summary>
internal static class CampaignEmails
{
    /// <summary>Tells a member they were given an army to command (if they want to know).</summary>
    public static async Task ArmyGivenAsync(
        WwgDbContext db,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        Guid armyId,
        CancellationToken cancellationToken
    )
    {
        var army = await db
            .Armies.AsNoTracking()
            .Where(a => a.Id == armyId && a.CommanderId != null)
            .Select(a => new
            {
                a.Name,
                a.CampaignId,
                Campaign = a.Campaign.Name,
                To = db
                    .CampaignMembers.Where(m => m.Id == a.CommanderId)
                    .Select(m => new TurnRecipient(
                        m.User.Email ?? "",
                        m.User.FirstName,
                        m.User.LastName,
                        m.User.MutedEmails
                    ))
                    .Single(),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (army is null || !army.To.Wants(EmailKind.ArmyGiven))
        {
            return;
        }

        await emails.QueueAsync(
            TurnEmails.Create(
                army.To,
                $"{army.Campaign}: you command {army.Name}",
                $"You've been given command of {army.Name} in {army.Campaign}.",
                "See your army on the map",
                TurnActionEndpoints.MapLink(appOptions, army.CampaignId)
            ),
            cancellationToken
        );
    }

    /// <summary>Tells the campaign's Umpires a player joined it.</summary>
    public static async Task PlayerJoinedAsync(
        WwgDbContext db,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        Guid campaignId,
        Guid userId,
        CancellationToken cancellationToken
    )
    {
        var joined = await db
            .Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.FirstName + " " + u.LastName)
            .SingleAsync(cancellationToken);
        var campaign = await db
            .Campaigns.AsNoTracking()
            .Where(c => c.Id == campaignId)
            .Select(c => c.Name)
            .SingleAsync(cancellationToken);
        var page = new Uri(
            appOptions.Value.PublicUrl!, // Required and validated at startup.
            $"/campaigns/{campaignId}"
        );
        foreach (
            var umpire in await UmpiresAsync(
                db,
                campaignId,
                EmailKind.PlayerJoined,
                cancellationToken
            )
        )
        {
            await emails.QueueAsync(
                TurnEmails.Create(
                    umpire,
                    $"{campaign}: {joined} joined",
                    $"{joined} joined {campaign} as a Player. Give them an army from the campaign's page.",
                    "See the campaign",
                    page
                ),
                cancellationToken
            );
        }
    }

    /// <summary>
    /// Once no army's turn in the open campaign turn is a Draft, tells the Umpires every army has
    /// submitted, with what's waiting for them as they start the next turn.
    /// </summary>
    public static async Task AllSubmittedAsync(
        WwgDbContext db,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var open = await db
            .CampaignTurns.AsNoTracking()
            .Where(t => t.CampaignId == campaignId && t.ClosedAt == null)
            .Select(t => new
            {
                t.Number,
                Campaign = t.Campaign.Name,
                Drafts = db.ArmyTurns.Count(a =>
                    a.CampaignTurnId == t.Id && a.Status == ArmyTurnStatus.Draft
                ),
                Armies = db.ArmyTurns.Count(a => a.CampaignTurnId == t.Id),
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (open is not { Drafts: 0, Armies: > 0 })
        {
            return;
        }

        var umpires = await UmpiresAsync(db, campaignId, EmailKind.AllSubmitted, cancellationToken);
        if (umpires.Count == 0)
        {
            return;
        }

        var waiting = await WaitingAsync(db, campaignId, cancellationToken);
        var armies = open.Armies == 1 ? "The army has" : $"All {open.Armies} armies have";
        var what = string.Create(
            CultureInfo.InvariantCulture,
            $"{armies} submitted turn {open.Number} of {open.Campaign}: approve them and start turn {open.Number + 1}."
        );
        foreach (var umpire in umpires)
        {
            await emails.QueueAsync(
                TurnEmails.Create(
                    umpire,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{open.Campaign}: every army has submitted turn {open.Number}"
                    ),
                    what,
                    "Review them on the map",
                    TurnActionEndpoints.MapLink(appOptions, campaignId),
                    waiting: waiting
                ),
                cancellationToken
            );
        }
    }

    /// <summary>What starting the next turn asks of the Umpire: sightings, couriers, attrition.</summary>
    private static async Task<List<string>> WaitingAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var sightings = (await SightingEndpoints.DueAsync(db, campaignId, cancellationToken)).Count;
        var couriers = (
            await IntelEndpoints.ListCouriersAsync(campaignId, db, cancellationToken)
        ).Value!.Count(c => c.AmongTheEnemy); // Ok always carries its list.
        var attrition = (await Attrition.DueAsync(db, campaignId, cancellationToken)).Count;
        var waiting = new List<string>();
        if (sightings > 0)
        {
            waiting.Add(Count(sightings, "sighting", "sightings") + " to shape");
        }
        if (couriers > 0)
        {
            waiting.Add(
                Count(couriers, "courier", "couriers")
                    + " among the enemy, which may be intercepted"
            );
        }
        if (attrition > 0)
        {
            waiting.Add($"Attrition to confirm for {Count(attrition, "unit", "units")}");
        }

        return waiting;
    }

    private static string Count(int n, string one, string many) =>
        string.Create(CultureInfo.InvariantCulture, $"{n} {(n == 1 ? one : many)}");

    private static async Task<List<TurnRecipient>> UmpiresAsync(
        WwgDbContext db,
        Guid campaignId,
        EmailKind kind,
        CancellationToken cancellationToken
    ) =>
        [
            .. (
                await db
                    .CampaignMembers.AsNoTracking()
                    .Where(m => m.CampaignId == campaignId && m.Role == CampaignRole.Umpire)
                    .Select(m => new TurnRecipient(
                        m.User.Email ?? "",
                        m.User.FirstName,
                        m.User.LastName,
                        m.User.MutedEmails
                    ))
                    .ToListAsync(cancellationToken)
            ).Where(u => u.Wants(kind)),
        ];
}
