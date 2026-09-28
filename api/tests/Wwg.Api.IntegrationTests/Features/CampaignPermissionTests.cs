using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// DESIGN.md §5.2, row by row: one theory per action, one case per role. A change to the table
/// there is a change here.
/// </summary>
public sealed class CampaignPermissionTests : ApiTest
{
    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ViewCampaign_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task EditCampaign_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                new UpdateCampaignRequest("Renamed", null),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task DeleteCampaign_ByRole_ReturnsExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .DeleteAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ViewJoinCode_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(CampaignUri(scenario, "/join-code"), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task RegenerateJoinCode_ByRole_ReturnsExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .PostAsync(CampaignUri(scenario, "/join-code"), null, CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ViewMembers_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(CampaignUri(scenario, "/members"), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task RemovePlayer_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .DeleteAsync(
                CampaignUri(scenario, $"/members/{scenario.PlayerMemberId}"),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    // "Self only (leave)": the Umpire can't leave, and an Admin who isn't a member has nothing to
    // leave.
    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NotFound)]
    [InlineData(Role.Umpire, HttpStatusCode.Conflict)]
    [InlineData(Role.Player, HttpStatusCode.NoContent)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task LeaveCampaign_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .DeleteAsync(CampaignUri(scenario, "/members/me"), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    // Site-wide admin actions (§5.2, admin table): anyone else gets 403, members or not.
    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.Forbidden)]
    public async Task ListAllCampaigns_ByRole_ReturnsExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(new Uri("/api/admin/campaigns", UriKind.Relative), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.Forbidden)]
    public async Task SetUmpire_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var playerId = await WithDbAsync(db =>
            Task.FromResult(db.CampaignMembers.Single(m => m.Id == scenario.PlayerMemberId).UserId)
        );

        using var response = await scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/admin/campaigns/{scenario.CampaignId}/umpire", UriKind.Relative),
                new SetUmpireRequest(playerId),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    private static Uri CampaignUri(CampaignScenario scenario, string path) =>
        new($"/api/campaigns/{scenario.CampaignId}{path}", UriKind.Relative);

    [Theory]
    [InlineData("get")]
    [InlineData("put")]
    [InlineData("delete")]
    public async Task AnyCampaignEndpoint_UnknownCampaign_Returns404EvenForAdmins(string method)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var uri = new Uri($"/api/campaigns/{Guid.CreateVersion7()}", UriKind.Relative);
        var admin = scenario.As(Role.Admin);

        using var response = method switch
        {
            "get" => await admin.GetAsync(uri, CancellationToken),
            "put" => await admin.PutAsJsonAsync(
                uri,
                new UpdateCampaignRequest("x", null),
                CancellationToken
            ),
            _ => await admin.DeleteAsync(uri, CancellationToken),
        };

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
