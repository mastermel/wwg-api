using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Supply;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Each army's depots (step 48a, decision 0019).</summary>
public sealed class DepotTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly SaveDepotRequest Charleroi = new(DepotKind.Main, "Charleroi", 0, 0);

    private static Task<HttpResponseMessage> CreateAsync(
        CampaignScenario scenario,
        SaveDepotRequest request,
        Role role = Role.Umpire,
        Guid? armyId = null
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{armyId ?? scenario.ArmyId}/depots", UriKind.Relative),
                request,
                Token
            );

    private static async Task<DepotResponse> CreatedAsync(CampaignScenario scenario)
    {
        using var response = await CreateAsync(scenario, Charleroi);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsAsync<DepotResponse>())!;
    }

    private static async Task<List<DepotResponse>> ListAsync(
        CampaignScenario scenario,
        Role role
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<DepotResponse>>($"/api/campaigns/{scenario.CampaignId}/depots")
        )!;

    [Fact]
    public async Task CreateDepot_InTheArea_IsTheArmys()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        var depot = await CreatedAsync(scenario);

        Assert.Equal(
            (scenario.ArmyId, DepotKind.Main, "Charleroi", 0, 0),
            (depot.ArmyId, depot.Kind, depot.Name, depot.Q, depot.R)
        );
        Assert.NotEqual(0, depot.Latitude);
    }

    [Fact]
    public async Task CreateDepot_OutsideTheArea_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var response = await CreateAsync(scenario, Charleroi with { Q = 90 });

        await response.AssertValidationProblemAsync("q");
    }

    [Fact]
    public async Task CreateDepot_WithoutAnArea_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, Charleroi);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateDepot_MovesAndChangesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        var depot = await CreatedAsync(scenario);

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/depots/{depot.Id}", UriKind.Relative),
                new SaveDepotRequest(DepotKind.Intermediate, " ", 1, 0),
                Token
            );
        var moved = Assert.Single(await ListAsync(scenario, Role.Umpire));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            (DepotKind.Intermediate, null, 1, 0),
            (moved.Kind, moved.Name, moved.Q, moved.R)
        );
    }

    [Fact]
    public async Task DeleteDepot_RemovesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        var depot = await CreatedAsync(scenario);

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(new Uri($"/api/depots/{depot.Id}", UriKind.Relative), Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await ListAsync(scenario, Role.Umpire));
    }

    [Theory]
    [InlineData(Role.Admin, 1)]
    [InlineData(Role.Umpire, 1)]
    [InlineData(Role.Commander, 1)]
    [InlineData(Role.Player, 0)]
    public async Task ListDepots_ByRole_TheArmysCommanderAndTheUmpire(Role role, int seen)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        await CreatedAsync(scenario);

        Assert.Equal(seen, (await ListAsync(scenario, role)).Count);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.Created)]
    [InlineData(Role.Umpire, HttpStatusCode.Created)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task CreateDepot_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var response = await CreateAsync(scenario, Charleroi, role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task DeleteDepot_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        var depot = await CreatedAsync(scenario);

        using var response = await scenario
            .As(role)
            .DeleteAsync(new Uri($"/api/depots/{depot.Id}", UriKind.Relative), Token);

        Assert.Equal(expected, response.StatusCode);
    }
}
