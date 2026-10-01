using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>A campaign's concentration settings: its limits, and which types count (step 46).</summary>
public sealed class ConcentrationTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static async Task<CampaignConcentrationResponse> ConcentrationAsync(
        CampaignScenario scenario
    ) =>
        (
            await scenario
                .As(Role.Player)
                .GetAsAsync<CampaignConcentrationResponse>(
                    $"/api/campaigns/{scenario.CampaignId}/concentration"
                )
        )!;

    private static Task<HttpResponseMessage> UpdateAsync(
        CampaignScenario scenario,
        UpdateCampaignConcentrationRequest request,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/concentration", UriKind.Relative),
                request,
                Token
            );

    [Fact]
    public async Task GetCampaignConcentration_Untouched_IsTheRulesLimitsAndTheUsualTypes()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var concentration = await ConcentrationAsync(scenario);

        Assert.Equal((200, 160), (concentration.InfantryLimit, concentration.CavalryLimit));
        Assert.Contains(UnitType.FootArtillery, concentration.InfantryTypes);
        Assert.Contains(UnitType.HorseArtillery, concentration.CavalryTypes);
        // Supply trains, siege artillery and boats are free.
        Assert.DoesNotContain(
            concentration.InfantryTypes.Concat(concentration.CavalryTypes),
            t => t is UnitType.SupplyTrain or UnitType.SiegeArtillery or UnitType.Boat
        );
    }

    [Fact]
    public async Task UpdateCampaignConcentration_SavesTheTypesAndLimits()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(
            scenario,
            new([UnitType.LineInfantry], [UnitType.HeavyCavalry, UnitType.SupplyTrain], 300, 120)
        );
        response.EnsureSuccessStatusCode();
        var concentration = await ConcentrationAsync(scenario);

        Assert.Equal([UnitType.LineInfantry], concentration.InfantryTypes);
        Assert.Equal([UnitType.HeavyCavalry, UnitType.SupplyTrain], concentration.CavalryTypes);
        Assert.Equal((300, 120), (concentration.InfantryLimit, concentration.CavalryLimit));
    }

    [Fact]
    public async Task UpdateCampaignConcentration_NoTypes_LeavesEveryTypeFree()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new([], [], 200, 160));
        response.EnsureSuccessStatusCode();
        var concentration = await ConcentrationAsync(scenario);

        Assert.Empty(concentration.InfantryTypes);
        Assert.Empty(concentration.CavalryTypes);
    }

    [Fact]
    public async Task UpdateCampaignConcentration_ATypeInBoth_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(
            scenario,
            new([UnitType.Scouts], [UnitType.Scouts], 200, 160)
        );

        await response.AssertValidationProblemAsync("cavalryTypes");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10_001)]
    public async Task UpdateCampaignConcentration_ALimitOutOfRange_IsAValidationError(int limit)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new([], [], limit, 160));

        await response.AssertValidationProblemAsync("infantryLimit");
    }

    [Fact]
    public async Task UpdateCampaignConcentration_AnUnknownType_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new([(UnitType)99], [], 200, 160));

        await response.AssertValidationProblemAsync("infantryTypes");
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateCampaignConcentration_ByRole_TheUmpire(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(scenario, new([], [], 200, 160), role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task GetCampaignConcentration_ByRole_EveryMember(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/concentration", UriKind.Relative),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
