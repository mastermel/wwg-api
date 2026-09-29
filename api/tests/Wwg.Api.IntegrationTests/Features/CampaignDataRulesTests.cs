using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Data.Entities;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The rules the database itself enforces (DESIGN.md §5.1), checked on real SQLite.</summary>
public sealed class CampaignDataRulesTests : ApiTest
{
    private async Task<AppUser> UserAsync(string email)
    {
        await using var scope = App.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = new AppUser
        {
            FirstName = "Test",
            LastName = "User",
            Email = email,
            UserName = email,
        };
        (await users.CreateAsync(user, TestPassword)).EnsureSucceeded();
        return user;
    }

    private async Task<Guid> CampaignAsync(params (Guid UserId, CampaignRole Role)[] members)
    {
        var campaign = new Campaign
        {
            Name = "Test campaign",
            JoinCode = Guid.CreateVersion7().ToString("N"),
        };
        campaign.Members.AddRange(
            members.Select(m => new CampaignMember { UserId = m.UserId, Role = m.Role })
        );
        await WithDbAsync(async db =>
        {
            db.Campaigns.Add(campaign);
            return await db.SaveChangesAsync(CancellationToken);
        });
        return campaign.Id;
    }

    private Task<int> AddMemberAsync(Guid campaignId, Guid userId, CampaignRole role) =>
        WithDbAsync(db =>
        {
            db.CampaignMembers.Add(
                new CampaignMember
                {
                    CampaignId = campaignId,
                    UserId = userId,
                    Role = role,
                }
            );
            return db.SaveChangesAsync(CancellationToken);
        });

    [Fact]
    public async Task SecondUmpire_IsRefusedByTheDatabase()
    {
        var first = await UserAsync("u1@example.com");
        var second = await UserAsync("u2@example.com");
        var id = await CampaignAsync((first.Id, CampaignRole.Umpire));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddMemberAsync(id, second.Id, CampaignRole.Umpire)
        );
    }

    [Fact]
    public async Task ManyPlayers_AreAllowed()
    {
        var umpire = await UserAsync("u@example.com");
        var p1 = await UserAsync("p1@example.com");
        var p2 = await UserAsync("p2@example.com");

        var id = await CampaignAsync(
            (umpire.Id, CampaignRole.Umpire),
            (p1.Id, CampaignRole.Player),
            (p2.Id, CampaignRole.Player)
        );

        Assert.Equal(
            3,
            await WithDbAsync(db =>
                db.CampaignMembers.CountAsync(m => m.CampaignId == id, CancellationToken)
            )
        );
    }

    [Fact]
    public async Task SameUserTwice_IsRefusedByTheDatabase()
    {
        var user = await UserAsync("u@example.com");
        var id = await CampaignAsync((user.Id, CampaignRole.Player));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            AddMemberAsync(id, user.Id, CampaignRole.Player)
        );
    }

    [Fact]
    public async Task DeletingACampaign_DeletesItsMembers()
    {
        var user = await UserAsync("u@example.com");
        var id = await CampaignAsync((user.Id, CampaignRole.Umpire));

        await WithDbAsync(db =>
            db.Campaigns.Where(c => c.Id == id).ExecuteDeleteAsync(CancellationToken)
        );

        Assert.Equal(0, await WithDbAsync(db => db.CampaignMembers.CountAsync(CancellationToken)));
    }

    [Fact]
    public async Task DeletingACampaign_DeletesItsArmiesAndTheirUnits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await WithDbAsync(db =>
            db.Campaigns.Where(c => c.Id == scenario.CampaignId)
                .ExecuteDeleteAsync(CancellationToken)
        );

        Assert.Equal(
            (0, 0),
            (
                await WithDbAsync(db => db.Armies.CountAsync(CancellationToken)),
                await WithDbAsync(db => db.Units.CountAsync(CancellationToken))
            )
        );
    }

    [Fact]
    public async Task DeletingAUser_DeletesTheirMembershipsButKeepsTheCampaign()
    {
        var umpire = await UserAsync("u@example.com");
        var id = await CampaignAsync((umpire.Id, CampaignRole.Umpire));

        await using (var scope = App.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
            (
                // Created at the top of this test.
                await users.DeleteAsync((await users.FindByIdAsync(umpire.Id.ToString()))!)
            ).EnsureSucceeded();
        }

        Assert.True(
            await WithDbAsync(db => db.Campaigns.AnyAsync(c => c.Id == id, CancellationToken))
        );
        Assert.Equal(0, await WithDbAsync(db => db.CampaignMembers.CountAsync(CancellationToken)));
    }
}
