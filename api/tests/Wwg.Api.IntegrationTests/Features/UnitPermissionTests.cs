using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Units;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// DESIGN.md §5.2's unit rows. Viewing an army's units is the "view an army" row
/// (<see cref="ArmyPermissionTests"/>): its details carry them.
/// </summary>
public sealed class UnitPermissionTests : ApiTest
{
    [Theory]
    [InlineData(Role.Admin, true)]
    [InlineData(Role.Umpire, true)]
    [InlineData(Role.Commander, true)]
    [InlineData(Role.Player, false)]
    [InlineData(Role.NonMember, false)]
    public async Task ViewUnits_ByRole_OnlyThoseWhoSeeTheArmy(Role role, bool expected)
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
    [InlineData(Role.Admin, HttpStatusCode.Created)]
    [InlineData(Role.Umpire, HttpStatusCode.Created)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task CreateUnit_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateUnitRequest("2nd Division", UnitType.LightInfantry, 4, 15),
                CancellationToken
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
                new UpdateUnitRequest("Light Division", UnitType.LightInfantry, 4, 15),
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
        var uri = new Uri($"/api/units/{Guid.CreateVersion7()}", UriKind.Relative);
        var admin = scenario.As(Role.Admin);

        using var response = method switch
        {
            "put" => await admin.PutAsJsonAsync(
                uri,
                new UpdateUnitRequest("x", UnitType.Skirmishers, 1, 0),
                CancellationToken
            ),
            _ => await admin.DeleteAsync(uri, CancellationToken),
        };

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    private static Uri UnitUri(CampaignScenario scenario) =>
        new($"/api/units/{scenario.UnitId}", UriKind.Relative);
}
