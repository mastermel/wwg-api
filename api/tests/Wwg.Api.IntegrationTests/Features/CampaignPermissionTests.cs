using System.Net;
using System.Net.Http.Json;
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
