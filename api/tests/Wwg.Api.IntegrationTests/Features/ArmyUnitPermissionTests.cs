using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// DESIGN.md §5.2's unit rows. Viewing an army's units is the "view an army" row
/// (<see cref="ArmyPermissionTests"/>): its details carry them.
/// </summary>
public sealed class ArmyUnitPermissionTests : ApiTest
{
    [Theory]
    [InlineData(Role.Admin, true)]
    [InlineData(Role.Umpire, true)]
    [InlineData(Role.Commander, true)]
    [InlineData(Role.Player, true)]
    [InlineData(Role.NonMember, false)]
    public async Task ViewUnits_ByRole_EveryMember(Role role, bool expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );

        var army = response.IsSuccessStatusCode
            ? await response.Content.ReadAsAsync<ArmyResponse>()
            : null;
        Assert.Equal(expected, army?.Units.Any(u => u.Id == scenario.UnitId) ?? false);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task AddUnits_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var unitId = await LibrarySteps.CreateUnitAsync(
            scenario.As(Role.Admin),
            scenario.FactionId,
            "2nd Division"
        );

        using var response = await LibrarySteps.AddAsync(
            scenario.As(role),
            scenario.ArmyId,
            unitId
        );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateUnit_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .PutAsJsonAsync(
                UnitUri(scenario),
                new UpdateArmyUnitRequest("Light Division", UnitType.LightInfantry, 4, 15),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task DeleteUnit_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .DeleteAsync(UnitUri(scenario), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("put")]
    [InlineData("delete")]
    public async Task AnyUnitEndpoint_UnknownUnit_Returns404EvenForAdmins(string method)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var uri = new Uri($"/api/army-units/{Guid.CreateVersion7()}", UriKind.Relative);
        var admin = scenario.As(Role.Admin);

        using var response = method switch
        {
            "put" => await admin.PutAsJsonAsync(
                uri,
                new UpdateArmyUnitRequest("x", UnitType.Partisans, 1, 0),
                CancellationToken
            ),
            _ => await admin.DeleteAsync(uri, CancellationToken),
        };

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private static Uri UnitUri(CampaignScenario scenario) =>
        new($"/api/army-units/{scenario.UnitId}", UriKind.Relative);

    [Theory]
    [InlineData(Role.Admin, 1)]
    [InlineData(Role.Umpire, 1)]
    [InlineData(Role.Commander, 1)]
    [InlineData(Role.Player, 1)]
    [InlineData(Role.NonMember, -1)]
    public async Task ListCampaignUnits_ByRole_EveryMemberSeesThemAll(Role role, int expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/units", UriKind.Relative),
                CancellationToken
            );

        if (expected < 0)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            return;
        }

        var units = await response.Content.ReadAsAsync<List<ArmyUnitResponse>>();
        Assert.Equal(scenario.UnitId, Assert.Single(units!).Id);
    }
}
