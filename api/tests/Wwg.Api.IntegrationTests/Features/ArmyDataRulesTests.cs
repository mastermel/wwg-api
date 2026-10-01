using Microsoft.Data.Sqlite;
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
    public async Task UmpireAsCommander_IsRefusedByTheDatabase()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var refused = await Assert.ThrowsAsync<SqliteException>(() =>
            WithDbAsync(db =>
                db.Armies.Where(a => a.Id == scenario.ArmyId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(a => a.CommanderId, scenario.UmpireMemberId),
                        CancellationToken
                    )
            )
        );
        Assert.Contains(
            "must be a Player in its campaign",
            refused.Message,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task CommanderFromAnotherCampaign_IsRefusedByTheDatabase()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // The spare Player also joins another campaign; that membership can't command here.
        var otherMembershipId = await WithDbAsync(async db =>
        {
            var userId = await db
                .CampaignMembers.Where(m => m.Id == scenario.PlayerMemberId)
                .Select(m => m.UserId)
                .SingleAsync(CancellationToken);
            var other = new Campaign { Name = "The Hundred Days", JoinCode = "other-join-code" };
            var membership = new CampaignMember
            {
                CampaignId = other.Id,
                UserId = userId,
                Role = CampaignRole.Player,
            };
            db.AddRange(other, membership);
            await db.SaveChangesAsync(CancellationToken);
            return membership.Id;
        });

        await Assert.ThrowsAsync<SqliteException>(() =>
            WithDbAsync(db =>
                db.Armies.Where(a => a.Id == scenario.ArmyId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(a => a.CommanderId, otherMembershipId),
                        CancellationToken
                    )
            )
        );
    }

    [Fact]
    public async Task PromotingACommanderToUmpire_IsRefusedByTheDatabase()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // No Umpire first, so the one-Umpire index isn't what refuses it.
        await WithDbAsync(db =>
            db.CampaignMembers.Where(m => m.Id == scenario.UmpireMemberId)
                .ExecuteDeleteAsync(CancellationToken)
        );

        var refused = await Assert.ThrowsAsync<SqliteException>(() =>
            WithDbAsync(db =>
                db.CampaignMembers.Where(m => m.Id == scenario.CommanderMemberId)
                    .ExecuteUpdateAsync(
                        s => s.SetProperty(m => m.Role, CampaignRole.Umpire),
                        CancellationToken
                    )
            )
        );
        Assert.Contains("can't command an army", refused.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManyUnassignedArmies_AreAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await WithDbAsync(db =>
        {
            db.Armies.AddRange(
                new Army
                {
                    CampaignId = scenario.CampaignId,
                    SideId = scenario.SideId,
                    Name = "Reserve",
                },
                new Army
                {
                    CampaignId = scenario.CampaignId,
                    SideId = scenario.SideId,
                    Name = "Garrison",
                }
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
