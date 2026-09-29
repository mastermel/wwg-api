using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Turns;

/// <summary>Where a campaign is: setting up (turn 0), or running (turn 1 or later is open).</summary>
public enum CampaignStage
{
    Setup,
    Running,
}

/// <summary>The rules of a campaign's turns that several features share (DESIGN.md §5.1).</summary>
internal static class TurnRules
{
    /// <summary>The open campaign turn, or null if there's none yet (a campaign before any setup).</summary>
    public static Task<CampaignTurn?> OpenTurnAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        db.CampaignTurns.SingleOrDefaultAsync(
            t => t.CampaignId == campaignId && t.ClosedAt == null,
            cancellationToken
        );

    /// <summary>Running once turn 1 has opened; setting up until then.</summary>
    public static Task<bool> HasStartedAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        db.CampaignTurns.AnyAsync(
            t => t.CampaignId == campaignId && t.Number > 0,
            cancellationToken
        );

    /// <summary>
    /// Turn 0, the setup turn, made the first time it's needed (campaigns start without turns).
    /// Null once the campaign has started. Added to the context, not saved.
    /// </summary>
    public static async Task<CampaignTurn?> SetupTurnAsync(
        WwgDbContext db,
        TimeProvider time,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var open = await OpenTurnAsync(db, campaignId, cancellationToken);
        if (open is not null)
        {
            return open.Number == 0 ? open : null;
        }

        if (await db.CampaignTurns.AnyAsync(t => t.CampaignId == campaignId, cancellationToken))
        {
            return null;
        }

        var setup = new CampaignTurn
        {
            CampaignId = campaignId,
            Number = 0,
            OpenedAt = time.GetUtcNow().UtcDateTime,
        };
        db.CampaignTurns.Add(setup);
        return setup;
    }

    /// <summary>The army's turn in a campaign turn, made (as a Draft) if it has none yet. Not saved.</summary>
    public static async Task<ArmyTurn> ArmyTurnAsync(
        WwgDbContext db,
        CampaignTurn campaignTurn,
        Guid armyId,
        CancellationToken cancellationToken
    )
    {
        var armyTurn =
            db.ArmyTurns.Local.SingleOrDefault(t =>
                t.CampaignTurnId == campaignTurn.Id && t.ArmyId == armyId
            )
            ?? await db.ArmyTurns.SingleOrDefaultAsync(
                t => t.CampaignTurnId == campaignTurn.Id && t.ArmyId == armyId,
                cancellationToken
            );
        if (armyTurn is null)
        {
            armyTurn = new ArmyTurn
            {
                CampaignTurnId = campaignTurn.Id,
                ArmyId = armyId,
                Status = ArmyTurnStatus.Draft,
            };
            db.ArmyTurns.Add(armyTurn);
        }

        return armyTurn;
    }
}
