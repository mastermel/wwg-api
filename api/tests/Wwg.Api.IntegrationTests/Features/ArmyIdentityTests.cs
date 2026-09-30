using System.Net;
using System.Net.Http.Json;
using System.Text;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Sides;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>An army's side, colour and nation, and the 8-army limit (DESIGN.md §5.1).</summary>
public sealed class ArmyIdentityTests : ApiTest
{
    private static Task<HttpResponseMessage> CreateAsync(
        CampaignScenario scenario,
        CreateArmyRequest request
    ) =>
        scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                request,
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> UpdateAsync(
        CampaignScenario scenario,
        UpdateArmyRequest request
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                request,
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task CreateArmy_WithoutAColour_GetsTheFirstOneNoArmyHas()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var second = await CreateAsync(scenario, new CreateArmyRequest("Second Corps", null));
        using var third = await CreateAsync(scenario, new CreateArmyRequest("Third Corps", null));

        // First Corps, the scenario's, has Red.
        Assert.Equal(
            (ArmyColor.Blue, ArmyColor.Green),
            (
                (await second.Content.ReadAsAsync<ArmyResponse>())!.Color,
                (await third.Content.ReadAsAsync<ArmyResponse>())!.Color
            )
        );
    }

    [Fact]
    public async Task CreateArmy_WithSideColourAndNation_SavesThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(
            scenario,
            new CreateArmyRequest(
                "Armée du Nord",
                null,
                scenario.SideId,
                ArmyColor.Blue,
                Nation.France
            )
        );

        var army = await response.Content.ReadAsAsync<ArmyResponse>();
        Assert.Equal(
            ("Coalition", ArmyColor.Blue, Nation.France),
            (army?.Side?.Name, army?.Color, army?.Nation)
        );
    }

    [Fact]
    public async Task CreateArmy_WithoutANation_HasNone()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, new CreateArmyRequest("Reserve", null));

        var army = await response.Content.ReadAsAsync<ArmyResponse>();
        Assert.Equal((Nation.None, null), (army?.Nation, army?.Side));
    }

    [Fact]
    public async Task CreateArmy_TheNinth_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        for (var i = 2; i <= Army.MaxPerCampaign; i++)
        {
            using var created = await CreateAsync(
                scenario,
                new CreateArmyRequest($"Corps {i}", null)
            );
            created.EnsureSuccessStatusCode();
        }

        using var response = await CreateAsync(scenario, new CreateArmyRequest("Ninth", null));

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CreateArmy_SideOfAnotherCampaign_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var elsewhere = await OtherCampaignsSideAsync(scenario);

        using var response = await CreateAsync(
            scenario,
            new CreateArmyRequest("Second Corps", null, elsewhere)
        );

        await response.AssertValidationProblemAsync("sideId");
    }

    [Fact]
    public async Task UpdateArmy_Always_SavesNameSideColourAndNation()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await UpdateAsync(
            scenario,
            new UpdateArmyRequest("Prussian I Corps", null, ArmyColor.Gold, Nation.Prussia)
        );

        var armies = await scenario
            .As(Role.Player)
            .GetAsAsync<List<ArmySummary>>($"/api/campaigns/{scenario.CampaignId}/armies");
        var army = Assert.Single(armies!);
        Assert.Equal(
            ("Prussian I Corps", null, ArmyColor.Gold, Nation.Prussia),
            (army.Name, army.Side, army.Color, army.Nation)
        );
    }

    [Fact]
    public async Task UpdateArmy_SideOfAnotherCampaign_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var elsewhere = await OtherCampaignsSideAsync(scenario);

        using var response = await UpdateAsync(
            scenario,
            new UpdateArmyRequest("First Corps", elsewhere, ArmyColor.Red, Nation.None)
        );

        await response.AssertValidationProblemAsync("sideId");
    }

    [Theory]
    [InlineData("""{ "name": "First Corps", "sideId": null, "nation": "France" }""")]
    [InlineData("""{ "name": "First Corps", "sideId": null, "color": "Red" }""")]
    public async Task UpdateArmy_ColourOrNationLeftOut_Returns400(string json)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new StringContent(json, Encoding.UTF8, "application/json"),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateArmy_UnknownColour_Returns400()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new StringContent(
                    """{ "name": "First Corps", "sideId": null, "color": "Beige", "nation": "None" }""",
                    Encoding.UTF8,
                    "application/json"
                ),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>A side in a campaign of the Umpire's that isn't the scenario's.</summary>
    private static async Task<Guid> OtherCampaignsSideAsync(CampaignScenario scenario)
    {
        var umpire = scenario.As(Role.Umpire);
        using var campaign = await umpire.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new Wwg.Api.Features.Campaigns.CreateCampaignRequest("Elsewhere", null),
            TestContext.Current.CancellationToken
        );
        var campaignId = (
            await campaign.Content.ReadAsAsync<Wwg.Api.Features.Campaigns.CampaignResponse>()
        )!.Id;
        using var side = await umpire.PostAsJsonAsync(
            new Uri($"/api/campaigns/{campaignId}/sides", UriKind.Relative),
            new CreateSideRequest("Elsewhere's side"),
            TestContext.Current.CancellationToken
        );
        return (await side.Content.ReadAsAsync<SideResponse>())!.Id;
    }
}
