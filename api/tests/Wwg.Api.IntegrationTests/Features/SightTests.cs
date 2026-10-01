using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Sightings;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// What units can see, for the sightings the Umpire shapes (step 49a, decision 0020). The
/// scenario's unit is at (0, 0); the other side's units are placed where each test says.
/// </summary>
public sealed class SightTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    private static async Task GroundAsync(CampaignScenario scenario, Hex hex, Terrain terrain)
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/cells/{hex.Q}/{hex.R}",
                    UriKind.Relative
                ),
                new UpdateHexCellRequest(terrain, false, HexSettlement.None),
                Token
            );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<Guid> EnemyArmyAsync(CampaignScenario scenario)
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Prussians", null, scenario.OtherSideId),
                Token
            );
        return (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
    }

    private static async Task EnemyAsync(
        CampaignScenario scenario,
        Guid army,
        Hex at,
        string name,
        UnitType type = UnitType.LineInfantry
    )
    {
        var unit = await LibrarySteps.AddUnitAsync(scenario, name, type, armyId: army);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, at);
        placed.EnsureSuccessStatusCode();
    }

    /// <summary>The hexes the scenario's army would see.</summary>
    private static async Task<List<SightingDueResponse>> SeenAsync(CampaignScenario scenario) =>
        [
            .. (
                await scenario
                    .As(Role.Umpire)
                    .GetAsAsync<List<SightingDueResponse>>(
                        $"/api/campaigns/{scenario.CampaignId}/sightings/due"
                    )
            )!.Where(s => s.ObservingArmyId == scenario.ArmyId),
        ];

    [Fact]
    public async Task ListSightingsDue_OnFlatGround_TheNextHex()
    {
        using var scenario = await StartedAsync();
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(1, 0), "Brigade");
        await EnemyAsync(scenario, enemy, new Hex(2, -1), "Far brigade");

        var seen = Assert.Single(await SeenAsync(scenario));

        Assert.Equal((1, 0), (seen.Q, seen.R));
        Assert.Equal("1 hex south-east of 1st Division", seen.Whereabouts);
        Assert.Equal("Brigade", Assert.Single(seen.Units).Name);
        Assert.False(seen.Screened);
    }

    [Fact]
    public async Task ListSightingsDue_TheEnemySeesBackToo()
    {
        using var scenario = await StartedAsync();
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(1, 0), "Brigade");

        var all = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<SightingDueResponse>>(
                $"/api/campaigns/{scenario.CampaignId}/sightings/due"
            );

        Assert.Contains(all!, s => s.ObservingArmyId == enemy && (s.Q, s.R) == (0, 0));
    }

    [Fact]
    public async Task ListSightingsDue_FromALowHill_TwoHexes()
    {
        using var scenario = await StartedAsync();
        await GroundAsync(scenario, TurnSteps.Start, Terrain.LowHill);
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(2, 0), "Brigade");
        await EnemyAsync(scenario, enemy, new Hex(3, 0), "Far brigade");

        Assert.Equal([(2, 0)], (await SeenAsync(scenario)).Select(s => (s.Q, s.R)));
    }

    [Fact]
    public async Task ListSightingsDue_AHillAsHighInTheWay_BlocksTheView()
    {
        using var scenario = await StartedAsync();
        await GroundAsync(scenario, TurnSteps.Start, Terrain.LowHill);
        await GroundAsync(scenario, new Hex(1, 0), Terrain.LowHill);
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(2, 0), "Brigade");

        Assert.Empty(await SeenAsync(scenario));
    }

    [Fact]
    public async Task ListSightingsDue_FromHigherGround_SeesOverALowerHill()
    {
        using var scenario = await StartedAsync();
        await GroundAsync(scenario, TurnSteps.Start, Terrain.HighHill);
        await GroundAsync(scenario, new Hex(1, 0), Terrain.LowHill);
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(2, 0), "Brigade");

        Assert.Equal([(2, 0)], (await SeenAsync(scenario)).Select(s => (s.Q, s.R)));
    }

    [Fact]
    public async Task ListSightingsDue_LightTroopsInTheWay_FlagAScreen()
    {
        using var scenario = await StartedAsync();
        await GroundAsync(scenario, TurnSteps.Start, Terrain.LowHill);
        var enemy = await EnemyArmyAsync(scenario);
        await EnemyAsync(scenario, enemy, new Hex(1, 0), "Hussars", UnitType.LightCavalry);
        await EnemyAsync(scenario, enemy, new Hex(2, 0), "Brigade");

        var seen = await SeenAsync(scenario);

        Assert.Equal(
            [(1, 0, false), (2, 0, true)],
            seen.OrderBy(s => s.Q).Select(s => (s.Q, s.R, s.Screened))
        );
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListSightingsDue_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/sightings/due", UriKind.Relative),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
