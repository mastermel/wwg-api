using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The library factions an army takes its units from (decision 0015).</summary>
public sealed class ArmyFactionTests : ApiTest
{
    private static Task<HttpResponseMessage> UpdateAsync(
        CampaignScenario scenario,
        IReadOnlyList<Guid>? factionIds
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new UpdateArmyRequest(
                    "First Corps",
                    scenario.SideId,
                    ArmyColor.Red,
                    Nation.None,
                    factionIds
                ),
                TestContext.Current.CancellationToken
            );

    private static async Task<List<string>> FactionNamesAsync(CampaignScenario scenario) =>
        [
            .. (
                await scenario
                    .As(Role.Player)
                    .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}")
            )!.Factions.Select(f => f.Name),
        ];

    [Fact]
    public async Task CreateArmy_WithFactions_ChoosesThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var british = await LibrarySteps.CreateFactionAsync(
            scenario.As(Role.Admin),
            "British",
            Nation.Britain
        );

        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest(
                    "Allies",
                    null,
                    scenario.SideId,
                    FactionIds: [scenario.FactionId, british]
                ),
                CancellationToken
            );

        var army = await response.Content.ReadAsAsync<ArmyResponse>();
        Assert.Equal(
            [
                new ArmyFactionResponse(british, "British", Nation.Britain),
                new ArmyFactionResponse(scenario.FactionId, "French", Nation.France),
            ],
            army!.Factions
        );
    }

    [Fact]
    public async Task CreateArmy_UnknownFaction_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest(
                    "Allies",
                    null,
                    scenario.SideId,
                    FactionIds: [Guid.CreateVersion7()]
                ),
                CancellationToken
            );

        await response.AssertValidationProblemAsync("factionIds");
    }

    [Fact]
    public async Task UpdateArmy_WithFactions_AddsAndDropsThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var admin = scenario.As(Role.Admin);
        var british = await LibrarySteps.CreateFactionAsync(admin, "British", Nation.Britain);
        var dutch = await LibrarySteps.CreateFactionAsync(admin, "Dutch", Nation.Holland);
        using var first = await UpdateAsync(scenario, [scenario.FactionId, british, british]);

        using var response = await UpdateAsync(scenario, [scenario.FactionId, dutch]);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["Dutch", "French"], await FactionNamesAsync(scenario));
    }

    [Fact]
    public async Task UpdateArmy_WithoutFactionIds_LeavesThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["French"], await FactionNamesAsync(scenario));
    }

    [Fact]
    public async Task UpdateArmy_DroppingAFactionItHasUnitsFrom_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, []);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal(["French"], await FactionNamesAsync(scenario));
    }

    [Fact]
    public async Task UpdateArmy_DroppingAFactionOnceItsUnitsAreGone_IsAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var removed = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/army-units/{scenario.UnitId}", UriKind.Relative),
                CancellationToken
            );

        using var response = await UpdateAsync(scenario, []);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await FactionNamesAsync(scenario));
    }

    [Fact]
    public async Task DeleteLibraryUnit_InACampaign_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var army = await scenario
            .As(Role.Umpire)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");

        using var response = await scenario
            .As(Role.Admin)
            .DeleteAsync(
                new Uri($"/api/units/{army!.Units[0].UnitId}", UriKind.Relative),
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteFaction_ChosenByAnArmy_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var british = await LibrarySteps.CreateFactionAsync(
            scenario.As(Role.Admin),
            "British",
            Nation.Britain
        );
        await LibrarySteps.ChooseFactionsAsync(scenario.As(Role.Umpire), scenario.ArmyId, british);

        using var response = await scenario
            .As(Role.Admin)
            .DeleteAsync(new Uri($"/api/factions/{british}", UriKind.Relative), CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }
}
