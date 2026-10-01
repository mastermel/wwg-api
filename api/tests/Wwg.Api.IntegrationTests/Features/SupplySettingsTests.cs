using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Supply;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Supply settings and living off the land (step 48b, decision 0019).</summary>
public sealed class SupplySettingsTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<CampaignSupplySettingsResponse> SettingsAsync(
        CampaignScenario scenario
    ) =>
        (
            await scenario
                .As(Role.Player)
                .GetAsAsync<CampaignSupplySettingsResponse>(
                    $"/api/campaigns/{scenario.CampaignId}/supply-settings"
                )
        )!;

    private static Task<HttpResponseMessage> UpdateAsync(
        CampaignScenario scenario,
        UpdateCampaignSupplySettingsRequest request,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/supply-settings", UriKind.Relative),
                request,
                Token
            );

    private static GiveOrderRequest HoldLivingOffTheLand =>
        new(OrderKind.Hold, null, LivesOffTheLand: true);

    [Fact]
    public async Task GetSupplySettings_Untouched_IsTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var settings = await SettingsAsync(scenario);

        Assert.Equal(1, settings.Reach);
        Assert.Equal(
            [UnitType.Partisans, UnitType.LightInfantry, UnitType.Scouts, UnitType.LightCavalry],
            settings.ExemptTypes
        );
        Assert.Equal([Nation.France], settings.OffTheLandNations);
    }

    [Fact]
    public async Task UpdateSupplySettings_SavesThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(
            scenario,
            new(3, [UnitType.Scouts], [Nation.France, Nation.Spain])
        );
        response.EnsureSuccessStatusCode();
        var settings = await SettingsAsync(scenario);

        Assert.Equal(3, settings.Reach);
        Assert.Equal([UnitType.Scouts], settings.ExemptTypes);
        Assert.Equal([Nation.France, Nation.Spain], settings.OffTheLandNations);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public async Task UpdateSupplySettings_AReachOutOfRange_IsAValidationError(int reach)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new(reach, [], []));

        await response.AssertValidationProblemAsync("reach");
    }

    [Fact]
    public async Task UpdateSupplySettings_NoNation_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new(1, [], [Nation.None]));

        await response.AssertValidationProblemAsync("offTheLandNations");
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateSupplySettings_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new(1, [], []), role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_AFrenchUnitLivingOffTheLand_IsSaved()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, HoldLivingOffTheLand);
        var order = (await TurnSteps.OpenArmyTurnAsync(scenario)).Orders.Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(order.LivesOffTheLand);
    }

    [Fact]
    public async Task GiveOrder_LivingOffTheLandWhereItsNationMayNot_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var settings = await UpdateAsync(scenario, new(1, [], [Nation.Spain]));
        await TurnSteps.StartedAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, HoldLivingOffTheLand);

        await response.AssertValidationProblemAsync("livesOffTheLand");
    }

    [Fact]
    public async Task GiveOrder_TheUmpireSettingItLivingOffTheLand_SaysSoInTheHistory()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            HoldLivingOffTheLand,
            role: Role.Umpire
        );
        var note = (await TurnSteps.OpenArmyTurnAsync(scenario))
            .History.Single()
            .UnitNotes.Single();

        Assert.Equal("Set to hold. Living off the land.", note.Text);
    }
}
