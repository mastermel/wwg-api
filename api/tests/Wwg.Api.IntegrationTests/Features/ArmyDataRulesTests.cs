using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The army rules the database itself enforces (DESIGN.md §5.1), on real SQLite.</summary>
public sealed class ArmyDataRulesTests : ApiTest
{
    [Fact]
    public async Task SecondArmyForTheSameCommander_IsRefusedByTheDatabase()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            WithDbAsync(db =>
            {
                db.Armies.Add(
                    new Army
                    {
                        CampaignId = scenario.CampaignId,
                        Name = "Second Corps",
                        CommanderId = scenario.CommanderMemberId,
                    }
                );
                return db.SaveChangesAsync(CancellationToken);
            })
        );
    }

    [Fact]
    public async Task ManyUnassignedArmies_AreAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await WithDbAsync(db =>
        {
            db.Armies.AddRange(
                new Army { CampaignId = scenario.CampaignId, Name = "Reserve" },
                new Army { CampaignId = scenario.CampaignId, Name = "Garrison" }
            );
            return db.SaveChangesAsync(CancellationToken);
        });

        Assert.Equal(3, await WithDbAsync(db => db.Armies.CountAsync(CancellationToken)));
    }

    [Fact]
    public async Task DeletingTheCommandersMembership_LeavesTheArmyUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await WithDbAsync(db =>
            db.CampaignMembers.Where(m => m.Id == scenario.CommanderMemberId)
                .ExecuteDeleteAsync(CancellationToken)
        );

        var army = await WithDbAsync(db =>
            db.Armies.SingleAsync(a => a.Id == scenario.ArmyId, CancellationToken)
        );
        Assert.Null(army.CommanderId);
    }

    [Fact]
    public async Task DeletingTheCampaign_DeletesItsArmies()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await WithDbAsync(db =>
            db.Campaigns.Where(c => c.Id == scenario.CampaignId)
                .ExecuteDeleteAsync(CancellationToken)
        );

        Assert.Equal(0, await WithDbAsync(db => db.Armies.CountAsync(CancellationToken)));
    }
}
