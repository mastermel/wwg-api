using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Units;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class UnitTests : ApiTest
{
    private static Task<HttpResponseMessage> CreateAsync(CampaignScenario scenario, string name) =>
        scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateUnitRequest(name),
                TestContext.Current.CancellationToken
            );

    private static async Task<List<string>> UnitNamesAsync(CampaignScenario scenario)
    {
        var army = await scenario
            .As(Role.Commander)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");
        return [.. army!.Units.Select(u => u.Name)];
    }

    [Fact]
    public async Task CreateUnit_Valid_AddsItToTheArmy()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, "  Light Division ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var unit = await response.Content.ReadAsAsync<UnitResponse>();
        Assert.Equal($"/api/units/{unit?.Id}", response.Headers.Location?.ToString());
        Assert.Equal(("Light Division", scenario.ArmyId), (unit?.Name, unit?.ArmyId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateUnit_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task GetArmy_ListsItsUnitsByNameIgnoringCase()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        (await CreateAsync(scenario, "cavalry brigade")).Dispose();
        (await CreateAsync(scenario, "Artillery")).Dispose();

        Assert.Equal(
            ["1st Division", "Artillery", "cavalry brigade"],
            await UnitNamesAsync(scenario)
        );
    }

    [Fact]
    public async Task RenameUnit_ChangesItsName()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/units/{scenario.UnitId}", UriKind.Relative),
                new RenameUnitRequest(" Light Division "),
                CancellationToken
            );

        Assert.Equal("Light Division", (await response.Content.ReadAsAsync<UnitResponse>())?.Name);
        Assert.Equal(["Light Division"], await UnitNamesAsync(scenario));
    }

    [Fact]
    public async Task DeleteUnit_RemovesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/units/{scenario.UnitId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await UnitNamesAsync(scenario));
    }

    [Fact]
    public async Task DeleteArmy_DeletesItsUnits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(0, await WithDbAsync(db => db.Units.CountAsync(CancellationToken)));
    }

    [Fact]
    public async Task UnassignCommander_KeepsTheUnits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/commander", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(1, await WithDbAsync(db => db.Units.CountAsync(CancellationToken)));
    }
}
