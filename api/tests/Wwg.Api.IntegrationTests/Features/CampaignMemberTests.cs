using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class CampaignMemberTests : ApiTest
{
    [Fact]
    public async Task ListMembers_ShowsTheUmpireFirstThenPlayersWithTheirArmies()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var members = await scenario
            .As(Role.Player)
            .GetAsAsync<List<CampaignMemberResponse>>(
                $"/api/campaigns/{scenario.CampaignId}/members"
            );

        // The Players have the same name, so they're in the order they joined (their IDs).
        Assert.Equal(
            [
                (scenario.UmpireMemberId, CampaignRole.Umpire, null),
                (scenario.CommanderMemberId, CampaignRole.Player, "First Corps"),
                (scenario.PlayerMemberId, CampaignRole.Player, null),
            ],
            members?.Select(m => (m.Id, m.Role, m.Army?.Name))
        );
        Assert.All(members!, m => Assert.Equal("Test", m.FirstName));
    }

    [Fact]
    public async Task RegenerateJoinCode_OldLinkStopsWorkingAndMembersStay()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var umpire = scenario.As(Role.Umpire);
        var before = await umpire.GetAsAsync<JoinCodeResponse>(
            $"/api/campaigns/{scenario.CampaignId}/join-code"
        );

        using var response = await umpire.PostAsync(
            new Uri($"/api/campaigns/{scenario.CampaignId}/join-code", UriKind.Relative),
            null,
            CancellationToken
        );

        var after = await response.Content.ReadAsAsync<JoinCodeResponse>();
        Assert.NotEqual(before?.JoinCode, after?.JoinCode, StringComparer.Ordinal);
        Assert.Equal(22, after?.JoinCode.Length);
        using var oldLink = await Client.GetAsync(
            new Uri($"/api/join/{before?.JoinCode}", UriKind.Relative),
            CancellationToken
        );
        await oldLink.AssertProblemAsync(HttpStatusCode.NotFound);
        using var newLink = await Client.GetAsync(
            new Uri($"/api/join/{after?.JoinCode}", UriKind.Relative),
            CancellationToken
        );
        Assert.Equal(HttpStatusCode.OK, newLink.StatusCode);
        Assert.Equal(3, await CountMembersAsync(scenario));
    }

    [Fact]
    public async Task LeaveCampaign_ByAPlayer_TheyLoseAccess()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var player = scenario.As(Role.Player);

        using var response = await player.DeleteAsync(
            new Uri($"/api/campaigns/{scenario.CampaignId}/members/me", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var details = await player.GetAsync(
            new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
            CancellationToken
        );
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
        Assert.Equal(2, await CountMembersAsync(scenario));
    }

    [Fact]
    public async Task LeaveCampaign_ByTheUmpire_Returns409AndTheyStay()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/members/me", UriKind.Relative),
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal(3, await CountMembersAsync(scenario));
    }

    [Fact]
    public async Task RemoveMember_APlayer_TheyLoseAccess()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/members/{scenario.PlayerMemberId}",
                    UriKind.Relative
                ),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        using var details = await scenario
            .As(Role.Player)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                CancellationToken
            );
        Assert.Equal(HttpStatusCode.NotFound, details.StatusCode);
    }

    [Fact]
    public async Task RemoveMember_TheUmpire_Returns409EvenForAnAdmin()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Admin)
            .DeleteAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/members/{scenario.UmpireMemberId}",
                    UriKind.Relative
                ),
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal(3, await CountMembersAsync(scenario));
    }

    [Fact]
    public async Task RemoveMember_OfAnotherCampaign_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var umpire = scenario.As(Role.Umpire);
        using var other = await umpire.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new CreateCampaignRequest("Another campaign", null),
            CancellationToken
        );
        var otherId = (await other.Content.ReadAsAsync<CampaignResponse>())?.Id;

        using var response = await umpire.DeleteAsync(
            new Uri(
                $"/api/campaigns/{otherId}/members/{scenario.PlayerMemberId}",
                UriKind.Relative
            ),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Equal(3, await CountMembersAsync(scenario));
    }

    private Task<int> CountMembersAsync(CampaignScenario scenario) =>
        WithDbAsync(db =>
            Task.FromResult(db.CampaignMembers.Count(m => m.CampaignId == scenario.CampaignId))
        );
}
