using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class AdminCampaignTests : ApiTest
{
    private Task<Guid> UserIdAsync(string email)
    {
        var normalized = email.ToUpperInvariant();
        return WithDbAsync(db =>
            Task.FromResult(db.Users.Single(u => u.NormalizedEmail == normalized).Id)
        );
    }

    private Task<List<(string Email, CampaignRole Role)>> MembersAsync(Guid campaignId) =>
        WithDbAsync(db =>
            Task.FromResult(
                db.CampaignMembers.Where(m => m.CampaignId == campaignId)
                    .OrderBy(m => m.User.Email)
                    .Select(m => new { m.User.Email, m.Role })
                    .AsEnumerable()
                    .Select(m => (m.Email ?? "", m.Role))
                    .ToList()
            )
        );

    private static Task<HttpResponseMessage> SetUmpireAsync(
        CampaignScenario scenario,
        Guid userId,
        Guid? campaignId = null
    ) =>
        scenario
            .As(Role.Admin)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/admin/campaigns/{campaignId ?? scenario.CampaignId}/umpire",
                    UriKind.Relative
                ),
                new SetUmpireRequest(userId),
                TestContext.Current.CancellationToken
            );

    private async Task DeleteUserAsync(CampaignScenario scenario, string email)
    {
        using var response = await scenario
            .As(Role.Admin)
            .DeleteAsync(
                new Uri($"/api/admin/users/{await UserIdAsync(email)}", UriKind.Relative),
                CancellationToken
            );
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task DeleteUser_TheUmpire_LeavesTheCampaignWithNoUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await DeleteUserAsync(scenario, "umpire@example.com");

        var campaign = await scenario
            .As(Role.Player)
            .GetAsAsync<CampaignResponse>($"/api/campaigns/{scenario.CampaignId}");
        Assert.Equal((null, 1), (campaign?.Umpire, campaign?.PlayerCount));
        Assert.Equal(
            [("player@example.com", CampaignRole.Player)],
            await MembersAsync(scenario.CampaignId)
        );
        // Only an Admin can manage it now.
        using var edit = await scenario
            .As(Role.Admin)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                new UpdateCampaignRequest("Still here", null),
                CancellationToken
            );
        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
    }

    [Fact]
    public async Task ListAllCampaigns_IncludesCampaignsWithNoUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");
        using var other = await scenario
            .As(Role.NonMember)
            .PostAsJsonAsync(
                new Uri("/api/campaigns", UriKind.Relative),
                new CreateCampaignRequest("austerlitz", null),
                CancellationToken
            );
        await DeleteUserAsync(scenario, "umpire@example.com");

        var all = await scenario
            .As(Role.Admin)
            .GetAsAsync<PagedResponse<AdminCampaignSummary>>("/api/admin/campaigns");
        var orphans = await scenario
            .As(Role.Admin)
            .GetAsAsync<PagedResponse<AdminCampaignSummary>>(
                "/api/admin/campaigns?withoutUmpire=true"
            );

        Assert.Equal(
            [("austerlitz", "Test User", 0), ("The Peninsular War", null, 1)],
            all?.Items.Select(c => (c.Name, c.UmpireName, c.PlayerCount))
        );
        Assert.Equal(scenario.CampaignId, Assert.Single(orphans!.Items).Id);
    }

    [Fact]
    public async Task ListAllCampaigns_Search_MatchesPartOfTheNameIgnoringCase()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        var found = await scenario
            .As(Role.Admin)
            .GetAsAsync<PagedResponse<AdminCampaignSummary>>("/api/admin/campaigns?search=PENIN");
        var none = await scenario
            .As(Role.Admin)
            .GetAsAsync<PagedResponse<AdminCampaignSummary>>("/api/admin/campaigns?search=russia");

        Assert.Equal(1, found?.TotalCount);
        Assert.Equal(0, none?.TotalCount);
    }

    [Fact]
    public async Task SetUmpire_APlayer_PromotesThemAndDemotesTheOldUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SetUmpireAsync(
            scenario,
            await UserIdAsync("player@example.com")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var campaign = await response.Content.ReadAsAsync<CampaignResponse>();
        Assert.Equal(scenario.PlayerMemberId, campaign?.Umpire?.MemberId);
        Assert.Null(campaign?.MyRole);
        Assert.Equal(
            [
                ("player@example.com", CampaignRole.Umpire),
                ("umpire@example.com", CampaignRole.Player),
            ],
            await MembersAsync(scenario.CampaignId)
        );
    }

    [Fact]
    public async Task SetUmpire_SomeoneNotInIt_AddsThemAsTheUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SetUmpireAsync(
            scenario,
            await UserIdAsync("outsider@example.com")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [
                ("outsider@example.com", CampaignRole.Umpire),
                ("player@example.com", CampaignRole.Player),
                ("umpire@example.com", CampaignRole.Player),
            ],
            await MembersAsync(scenario.CampaignId)
        );
    }

    [Fact]
    public async Task SetUmpire_ACampaignWithNoUmpire_GivesItOne()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await DeleteUserAsync(scenario, "umpire@example.com");

        using var response = await SetUmpireAsync(scenario, await UserIdAsync("admin@example.com"));

        var campaign = await response.Content.ReadAsAsync<CampaignResponse>();
        Assert.Equal(CampaignRole.Umpire, campaign?.MyRole);
        Assert.Equal("Test", campaign?.Umpire?.FirstName);
    }

    [Fact]
    public async Task SetUmpire_TheCurrentUmpire_ChangesNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var before = await MembersAsync(scenario.CampaignId);

        using var response = await SetUmpireAsync(
            scenario,
            await UserIdAsync("umpire@example.com")
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await MembersAsync(scenario.CampaignId));
    }

    [Fact]
    public async Task SetUmpire_UnknownUser_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SetUmpireAsync(scenario, Guid.CreateVersion7());

        await response.AssertValidationProblemAsync("userId");
    }

    [Fact]
    public async Task SetUmpire_UnknownCampaign_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SetUmpireAsync(
            scenario,
            await UserIdAsync("player@example.com"),
            Guid.CreateVersion7()
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetUser_ShowsTheirCampaignsAndRoles()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        var user = await scenario
            .As(Role.Admin)
            .GetAsAsync<UserDetails>($"/api/admin/users/{await UserIdAsync("player@example.com")}");

        Assert.Equal(
            [new UserCampaign(scenario.CampaignId, "The Peninsular War", CampaignRole.Player)],
            user?.Campaigns
        );
    }
}
