using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Supply;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Each unit's supply (step 48c, decision 0019). The scenario's unit is at (0, 0); a road runs
/// from (1, 0) by (2, −1) to (3, −1), where its army's depot is, when the test lays it.
/// </summary>
public sealed class SupplyTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Hex DepotHex = new(3, -1);
    private static readonly Hex OnTheRoad = new(2, -1);

    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    private static async Task RoadAsync(CampaignScenario scenario)
    {
        foreach (var (hex, side) in new[] { (new Hex(1, 0), "NE"), (OnTheRoad, "SE") })
        {
            using var response = await scenario
                .As(Role.Umpire)
                .PutAsJsonAsync(
                    new Uri(
                        $"/api/campaigns/{scenario.CampaignId}/grid/edges/{hex.Q}/{hex.R}/{side}",
                        UriKind.Relative
                    ),
                    new UpdateHexEdgeRequest(RoadQuality.Good, false, false, Waterway.None),
                    Token
                );
            response.EnsureSuccessStatusCode();
        }
    }

    private static async Task<Guid> DepotAsync(
        CampaignScenario scenario,
        Hex at,
        DepotKind kind = DepotKind.Main,
        Guid? armyId = null
    )
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{armyId ?? scenario.ArmyId}/depots", UriKind.Relative),
                new SaveDepotRequest(kind, null, at.Q, at.R),
                Token
            );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsAsync<DepotResponse>())!.Id;
    }

    /// <summary>An army of the other side with one unit of these points, placed in the hex.</summary>
    private static async Task EnemyAsync(
        CampaignScenario scenario,
        Hex at,
        int points,
        UnitType type = UnitType.LineInfantry
    )
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Prussians", null, scenario.OtherSideId),
                Token
            );
        var army = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var unit = await LibrarySteps.AddUnitAsync(
            scenario,
            "Brigade",
            type,
            points: points,
            armyId: army
        );
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, at);
        placed.EnsureSuccessStatusCode();
    }

    private static async Task<CampaignSupplyResponse> SupplyAsync(
        CampaignScenario scenario,
        Role role = Role.Commander
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<CampaignSupplyResponse>($"/api/campaigns/{scenario.CampaignId}/supply")
        )!;

    private static async Task<UnitSupplyResponse> UnitAsync(CampaignScenario scenario) =>
        (await SupplyAsync(scenario)).Units.Single(u => u.UnitId == scenario.UnitId);

    [Fact]
    public async Task GetSupply_AnArmyWithNoDepots_IsUntracked()
    {
        using var scenario = await StartedAsync();

        Assert.Equal(SupplyState.Untracked, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_NextToItsDepot_IsSupplied()
    {
        using var scenario = await StartedAsync();
        var depot = await DepotAsync(scenario, new Hex(1, 0));

        var supply = await UnitAsync(scenario);

        Assert.Equal((SupplyState.Supplied, depot), (supply.State, supply.DepotId));
    }

    [Fact]
    public async Task GetSupply_FarFromAnyRoute_IsUnsupplied()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        Assert.Equal(SupplyState.Unsupplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_ByRoadFromADistantDepot_IsSupplied()
    {
        using var scenario = await StartedAsync();
        await RoadAsync(scenario);
        await DepotAsync(scenario, DepotHex);

        Assert.Equal(SupplyState.Supplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_FiveEnemyPointsOnTheRoad_CutIt()
    {
        using var scenario = await StartedAsync();
        await RoadAsync(scenario);
        await DepotAsync(scenario, DepotHex);

        await EnemyAsync(scenario, OnTheRoad, points: 5);

        Assert.Equal(SupplyState.Unsupplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task StartNextTurn_OutOfSupply_IsInTheCommandersTurnEmail()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        await TurnSteps.PlayAsync(scenario, "hold", "hold");

        var second = await Emails.WaitForEmailToAsync(
            "commander@example.com",
            "turn 2 has started"
        );
        Assert.Contains(
            "- 1st Division is out of supply.",
            second.TextBody,
            StringComparison.Ordinal
        );
        var third = await Emails.WaitForEmailToAsync("commander@example.com", "turn 3 has started");
        Assert.Contains(
            "- 1st Division: 2nd turn out of supply.",
            third.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task GetSupply_EnemyBoatsOnTheRoad_DontCutIt()
    {
        using var scenario = await StartedAsync();
        await RoadAsync(scenario);
        await DepotAsync(scenario, DepotHex);

        await EnemyAsync(scenario, OnTheRoad, points: 30, UnitType.Boat);

        Assert.Equal(SupplyState.Supplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_TwiceTheEnemyInTheHex_KeepsTheRoadOpen()
    {
        using var scenario = await StartedAsync();
        await RoadAsync(scenario);
        await DepotAsync(scenario, DepotHex);
        await EnemyAsync(scenario, OnTheRoad, points: 10);
        var guard = await LibrarySteps.AddUnitAsync(scenario, "Guard", points: 20);
        using var placed = await TurnSteps.PlaceAsync(scenario, guard, OnTheRoad);

        Assert.Equal(SupplyState.Supplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_AnotherArmysDepot_DoesntSupplyIt()
    {
        using var scenario = await StartedAsync();
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Allies", null, scenario.SideId),
                Token
            );
        var ally = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        await DepotAsync(scenario, DepotHex);
        await DepotAsync(scenario, new Hex(1, 0), armyId: ally);

        Assert.Equal(SupplyState.Unsupplied, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_AnExemptType_IsExempt()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);
        using var settings = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/supply-settings", UriKind.Relative),
                new UpdateCampaignSupplySettingsRequest(1, [UnitType.LineInfantry], []),
                Token
            );

        Assert.Equal(SupplyState.Exempt, (await UnitAsync(scenario)).State);
    }

    [Fact]
    public async Task GetSupply_LivingOffTheLandThisTurn_IsSoNext()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Hold, null, LivesOffTheLand: true)
        );
        var supply = await UnitAsync(scenario);

        Assert.Equal(
            (SupplyState.Unsupplied, SupplyState.LivingOffTheLand, 0),
            (supply.State, supply.NextState, supply.NextUnsuppliedTurns)
        );
    }

    [Fact]
    public async Task StartNextTurn_OutOfSupply_CountsTheTurns()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        await TurnSteps.PlayAsync(scenario, "hold", "hold");
        var supply = await UnitAsync(scenario);

        Assert.Equal((2, 3), (supply.UnsuppliedTurns, supply.NextUnsuppliedTurns));
    }

    [Fact]
    public async Task StartNextTurn_BackInSupply_StartsTheCountAgain()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);
        await TurnSteps.PlayAsync(scenario, "hold");

        await DepotAsync(scenario, new Hex(1, 0));
        await TurnSteps.PlayAsync(scenario, "hold");

        Assert.Equal(0, (await UnitAsync(scenario)).UnsuppliedTurns);
    }

    [Fact]
    public async Task GetSupply_AnIntermediateDepotCutOff_SuppliesFromItsStockFor15Turns()
    {
        using var scenario = await StartedAsync();
        var depot = await DepotAsync(scenario, new Hex(1, 0), DepotKind.Intermediate);

        var stocked = await SupplyAsync(scenario);
        await WithDbAsync(db =>
            db.Depots.Where(d => d.Id == depot)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.CutOffTurns, 15), Token)
        );
        var spent = await UnitAsync(scenario);

        Assert.Equal(
            SupplyState.Supplied,
            stocked.Units.Single(u => u.UnitId == scenario.UnitId).State
        );
        Assert.False(Assert.Single(stocked.Depots).Connected);
        Assert.Equal(SupplyState.Unsupplied, spent.State);
    }

    [Fact]
    public async Task GetSupply_AnIntermediateDepotOnARouteFromAMainOne_IsConnected()
    {
        using var scenario = await StartedAsync();
        await RoadAsync(scenario);
        await DepotAsync(scenario, DepotHex);
        await DepotAsync(scenario, new Hex(1, 0), DepotKind.Intermediate);

        Assert.True(Assert.Single((await SupplyAsync(scenario)).Depots).Connected);
    }

    [Fact]
    public async Task StartNextTurn_AnIntermediateDepotCutOff_CountsTheTurns()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, new Hex(1, 0), DepotKind.Intermediate);

        await TurnSteps.PlayAsync(scenario, "hold", "hold");

        Assert.Equal(
            2,
            Assert.Single((await SupplyAsync(scenario, Role.Umpire)).Depots).CutOffTurns
        );
    }

    [Fact]
    public async Task ListAttritionDue_SixTurnsOutOfSupply_IsNothingYet()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        await TurnSteps.PlayAsync(scenario, "hold", "hold", "hold", "hold", "hold");

        Assert.Empty(await TurnSteps.AttritionDueAsync(scenario));
    }

    [Fact]
    public async Task ListAttritionDue_TheSeventhTurnOutOfSupply_IsNormalAttrition()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        await TurnSteps.PlayAsync(scenario, "hold", "hold", "hold", "hold", "hold", "hold");
        var due = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));

        Assert.Equal((7, 0, 1), (due.UnsuppliedTurns, due.ForcedMarchMultiplier, due.Multiplier));
    }

    [Fact]
    public async Task ListAttritionDue_AForcedMarchOutOfSupply_IsDoubled()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);
        await TurnSteps.PlayAsync(scenario, "move", "move", "move");
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        var there =
            await TurnSteps.HereAsync(scenario) == TurnSteps.Start
                ? new Hex(1, 0)
                : TurnSteps.Start;

        using var moved = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(there));
        var due = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));

        Assert.Equal(
            (2, 4, 2),
            (due.ForcedMarchTurns, due.UnsuppliedTurns, due.ForcedMarchMultiplier)
        );
    }

    [Fact]
    public async Task StartNextTurn_SupplysAttrition_SaysSoInTheHistory()
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);
        await TurnSteps.PlayAsync(scenario, "hold", "hold", "hold", "hold", "hold", "hold");
        await TurnSteps.CompletedAsync(scenario);

        using var started = await TurnSteps.StartNextTurnAsync(
            scenario,
            request: new([new AttritionLossRequest(scenario.UnitId, 1)])
        );
        started.EnsureSuccessStatusCode();
        var history = await scenario
            .As(Role.Player)
            .GetAsAsync<List<Wwg.Api.Features.ArmyUnits.PointsChangeResponse>>(
                $"/api/army-units/{scenario.UnitId}/points"
            );

        Assert.Equal("Out of supply, turn 7", Assert.Single(history!).Note);
    }

    [Theory]
    [InlineData(Role.Umpire, 1)]
    [InlineData(Role.Commander, 1)]
    [InlineData(Role.Player, 0)]
    public async Task GetSupply_ByRole_TheArmysCommanderAndTheUmpire(Role role, int seen)
    {
        using var scenario = await StartedAsync();
        await DepotAsync(scenario, DepotHex);

        Assert.Equal(seen, (await SupplyAsync(scenario, role)).Units.Count);
    }

    [Fact]
    public async Task GetSupply_ByANonMember_Returns404()
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(Role.NonMember)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/supply", UriKind.Relative),
                Token
            );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
