using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Features.Victory;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Towns and victory points (step 50, decision 0021). The scenario's unit is at (0, 0); an enemy
/// army of the other side, with no commander, has a brigade at (2, -1); towns as each test says.
/// </summary>
public sealed class VictoryTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Hex Wavre = new(0, 0);

    private static HexSettlement Town(string name, int? points = null) =>
        new(SettlementSize.Town, false, false, CapitalStatus.None, name, points);

    private static async Task<HttpResponseMessage> SettleAsync(
        CampaignScenario scenario,
        Hex at,
        HexSettlement settlement
    ) =>
        await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/cells/{at.Q}/{at.R}",
                    UriKind.Relative
                ),
                new UpdateHexCellRequest(Terrain.Flat, false, settlement),
                Token
            );

    private static Task<HttpResponseMessage> HoldAsync(
        CampaignScenario scenario,
        Hex at,
        Guid? army
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/holdings/{at.Q}/{at.R}",
                    UriKind.Relative
                ),
                new SetHoldingRequest(army),
                Token
            );

    /// <summary>Set up (the area, the scenario's unit placed, the enemy's brigade at <paramref name="enemyAt"/>), not started.</summary>
    private async Task<(CampaignScenario Scenario, Guid Enemy)> ReadyAsync(Hex enemyAt)
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.ReadyAsync(scenario);
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Prussians", null, scenario.OtherSideId),
                Token
            );
        var enemy = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var unit = await LibrarySteps.AddUnitAsync(scenario, "Brigade", armyId: enemy);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, enemyAt);
        placed.EnsureSuccessStatusCode();
        return (scenario, enemy);
    }

    private static async Task StartAsync(CampaignScenario scenario)
    {
        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
    }

    /// <summary>Every army holds, approved by the Umpire, and the next turn starts.</summary>
    private static async Task NextTurnAsync(
        CampaignScenario scenario,
        Dictionary<Guid, Hex>? moves = null
    )
    {
        var umpire = scenario.As(Role.Umpire);
        var armies = await umpire.GetAsAsync<List<ArmySummary>>(
            $"/api/campaigns/{scenario.CampaignId}/armies"
        );
        var units = await umpire.GetAsAsync<List<ArmyUnitResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/units"
        );
        foreach (var army in armies!)
        {
            var turn = (
                await umpire.GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{army.Id}/turns")
            )!.Single(t => t.Open);
            foreach (var unit in units!.Where(u => u.ArmyId == army.Id))
            {
                var order = moves?.GetValueOrDefault(unit.Id) is { } to
                    ? TurnSteps.Move(to)
                    : TurnSteps.Hold;
                using var given = await TurnSteps.OrderAsync(
                    scenario,
                    turn.Id,
                    order,
                    unit.Id,
                    Role.Umpire
                );
                given.EnsureSuccessStatusCode();
            }
            using var submitted = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "submit",
                Role.Umpire
            );
            using var approved = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "approve",
                Role.Umpire
            );
            approved.EnsureSuccessStatusCode();
        }
        using var next = await TurnSteps.StartNextTurnAsync(scenario, request: new([], []));
        next.EnsureSuccessStatusCode();
    }

    private static async Task<ScoreboardResponse> ScoreAsync(
        CampaignScenario scenario,
        Role role = Role.Umpire
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<ScoreboardResponse>($"/api/campaigns/{scenario.CampaignId}/scoreboard")
        )!;

    [Theory]
    [InlineData(SettlementSize.Town, false, false, CapitalStatus.None, 10)]
    [InlineData(SettlementSize.City, false, false, CapitalStatus.None, 25)]
    [InlineData(SettlementSize.Town, true, false, CapitalStatus.None, 35)]
    [InlineData(SettlementSize.None, false, true, CapitalStatus.None, 50)]
    [InlineData(SettlementSize.City, true, true, CapitalStatus.None, 50)]
    [InlineData(SettlementSize.City, false, false, CapitalStatus.Capital, 50)]
    [InlineData(SettlementSize.Town, false, false, CapitalStatus.Minor, 20)]
    public async Task GetScoreboard_ASettlementsValue_IsTheHighestThatApplies_PlusACapitals(
        SettlementSize size,
        bool walled,
        bool fortress,
        CapitalStatus capital,
        int value
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        using var settled = await SettleAsync(
            scenario,
            Wavre,
            new(size, walled, fortress, capital, "Wavre")
        );

        Assert.Equal(value, Assert.Single((await ScoreAsync(scenario)).Settlements).Value);
    }

    [Fact]
    public async Task GetScoreboard_TheUmpiresValue_ReplacesTheRules_AndNoneLeavesItOut()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        using var wavre = await SettleAsync(scenario, Wavre, Town("Wavre", 40));
        using var ligny = await SettleAsync(scenario, new Hex(1, 0), Town("Ligny", 0));

        Assert.Equal(
            [("Wavre", 40)],
            (await ScoreAsync(scenario)).Settlements.Select(s => (s.Name, s.Value))
        );
    }

    [Fact]
    public async Task UpdateHexCell_PointsWithoutASettlement_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var response = await SettleAsync(
            scenario,
            Wavre,
            HexSettlement.None with
            {
                VictoryPoints = 5,
            }
        );

        await response.AssertValidationProblemAsync("settlement");
    }

    [Fact]
    public async Task SetHolding_TheStartingHolder_CountsForItsSide()
    {
        var (scenario, _) = await ReadyAsync(new Hex(2, -1));
        using var _s = scenario;
        using var settled = await SettleAsync(scenario, Wavre, Town("Wavre"));

        using var held = await HoldAsync(scenario, Wavre, scenario.ArmyId);
        var score = await ScoreAsync(scenario, Role.Player);

        Assert.Equal(HttpStatusCode.NoContent, held.StatusCode);
        Assert.Equal([10, 0], score.Sides.Select(s => s.Points));
        Assert.Equal(10, score.Sides[0].Armies.Single(a => a.ArmyId == scenario.ArmyId).Points);
        // A Player without an army knows the totals, not who holds what.
        Assert.Empty(score.Settlements);
    }

    [Fact]
    public async Task SetHolding_WhereNothingsWorthAnything_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var held = await HoldAsync(scenario, Wavre, scenario.ArmyId);

        Assert.Equal(HttpStatusCode.NotFound, held.StatusCode);
    }

    [Fact]
    public async Task StartNextTurn_AnArmyAloneInTheHex_TakesIt()
    {
        var (scenario, enemy) = await ReadyAsync(new Hex(2, -1));
        using var _s = scenario;
        using var settled = await SettleAsync(scenario, new Hex(2, -1), Town("Ligny"));
        using var held = await HoldAsync(scenario, new Hex(2, -1), scenario.ArmyId);
        await StartAsync(scenario);

        await NextTurnAsync(scenario);
        var score = await ScoreAsync(scenario);

        Assert.Equal(enemy, Assert.Single(score.Settlements).ArmyId);
        Assert.Equal([0, 10], score.Sides.Select(s => s.Points));
        Assert.Equal(
            [(0, (Guid?)null, (Guid?)scenario.ArmyId, true), (1, scenario.ArmyId, enemy, false)],
            score.Changes.Select(c => (c.Turn, c.FromArmyId, c.ToArmyId, c.ByUmpire))
        );
        Assert.Equal(
            [(0, 10, 0), (1, 0, 10), (2, 0, 10)],
            score.Turns.Select(t => (t.Turn, t.Sides[0].Points, t.Sides[1].Points))
        );
    }

    [Fact]
    public async Task StartNextTurn_BothSidesInTheHex_LeavesItWithItsHolder()
    {
        // The enemy brigade starts in the scenario's unit's hex, which the scenario's army holds.
        var (scenario, _) = await ReadyAsync(Wavre);
        using var _s = scenario;
        using var settled = await SettleAsync(scenario, Wavre, Town("Wavre"));
        using var held = await HoldAsync(scenario, Wavre, scenario.ArmyId);
        await StartAsync(scenario);

        await NextTurnAsync(scenario);

        Assert.Equal(
            scenario.ArmyId,
            Assert.Single((await ScoreAsync(scenario)).Settlements).ArmyId
        );
    }

    [Fact]
    public async Task GetScoreboard_ACommander_SeesTheirSidesSettlements_AndChanges_NotTheEnemys()
    {
        var (scenario, enemy) = await ReadyAsync(new Hex(2, -1));
        using var _s = scenario;
        using var wavre = await SettleAsync(scenario, Wavre, Town("Wavre"));
        using var ligny = await SettleAsync(scenario, new Hex(2, -1), Town("Ligny"));
        using var ours = await HoldAsync(scenario, Wavre, scenario.ArmyId);
        using var theirs = await HoldAsync(scenario, new Hex(2, -1), enemy);

        var score = await ScoreAsync(scenario, Role.Commander);

        Assert.Equal("Wavre", Assert.Single(score.Settlements).Name);
        Assert.Equal([scenario.ArmyId], score.Changes.Select(c => c.ToArmyId!.Value));
        Assert.Equal([10, 10], score.Sides.Select(s => s.Points));
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task SetHolding_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        using var settled = await SettleAsync(scenario, Wavre, Town("Wavre"));

        using var held = await scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/holdings/0/0", UriKind.Relative),
                new SetHoldingRequest(scenario.ArmyId),
                Token
            );

        Assert.Equal(expected, held.StatusCode);
    }

    [Fact]
    public async Task GetScoreboard_ByANonMember_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.NonMember)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/scoreboard", UriKind.Relative),
                Token
            );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
