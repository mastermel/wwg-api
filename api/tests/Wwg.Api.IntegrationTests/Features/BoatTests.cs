using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Boats;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Features.Victory;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Units on boats (step 51, decision 0022). The scenario's unit (20 points: two boats' worth) is at
/// (0, 0), on a river's course flowing east through (1, 0), (2, 0) and (3, 0); a river runs along
/// (0, 0)'s north side. The army's boats are at (0, 0) too, as each test says.
/// </summary>
public sealed class BoatTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Hex Here = TurnSteps.Start;
    private static readonly Hex North = new(0, -1);
    private static readonly Hex South = new(0, 1);

    private static GiveOrderRequest Embark => new(OrderKind.Embark, null);

    private static GiveOrderRequest Disembark(params Hex[] to) => new(OrderKind.Disembark, to);

    private static async Task EdgeAsync(
        CampaignScenario scenario,
        Hex hex,
        string side,
        UpdateHexEdgeRequest edge
    )
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/edges/{hex.Q}/{hex.R}/{side}",
                    UriKind.Relative
                ),
                edge,
                Token
            );
        response.EnsureSuccessStatusCode();
    }

    /// <summary>The river, the unit and <paramref name="boats"/> boats at (0, 0), and turn 1 open.</summary>
    private async Task<(CampaignScenario Scenario, List<Guid> Boats)> AfloatAsync(int boats = 2)
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.ReadyAsync(scenario);
        var downstream = new UpdateHexEdgeRequest(RoadQuality.None, false, false, Waterway.Out);
        foreach (var q in new[] { 0, 1, 2 })
        {
            await EdgeAsync(scenario, new Hex(q, 0), "SE", downstream);
        }
        await EdgeAsync(
            scenario,
            Here,
            "N",
            new UpdateHexEdgeRequest(RoadQuality.None, true, false, Waterway.None)
        );

        var ids = new List<Guid>();
        for (var i = 0; i < boats; i++)
        {
            var boat = await LibrarySteps.AddUnitAsync(
                scenario,
                $"Boat {(char)('A' + i)}",
                UnitType.Boat,
                points: 0
            );
            using var placed = await TurnSteps.PlaceAsync(scenario, boat);
            placed.EnsureSuccessStatusCode();
            ids.Add(boat);
        }

        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return (scenario, ids);
    }

    /// <summary>The unit embarked in turn 1; turn 2 open.</summary>
    private async Task<(CampaignScenario Scenario, List<Guid> Boats)> AboardAsync(int boats = 2)
    {
        var (scenario, ids) = await AfloatAsync(boats);
        await TurnAsync(scenario, Embark);
        return (scenario, ids);
    }

    /// <summary>The unit's order in the open turn, and the turn played through to the next.</summary>
    private static async Task TurnAsync(CampaignScenario scenario, GiveOrderRequest order)
    {
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, order);
        given.EnsureSuccessStatusCode();
        // Every boat without an order (not tied to the unit) holds.
        foreach (var free in await FreeBoatsAsync(scenario))
        {
            using var held = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold, free);
        }
        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);
        submitted.EnsureSuccessStatusCode();
        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);
        approved.EnsureSuccessStatusCode();
        var due = await TurnSteps.AttritionDueAsync(scenario);
        using var next = await TurnSteps.StartNextTurnAsync(
            scenario,
            request: new([.. due.Select(d => new AttritionLossRequest(d.UnitId, d.Loss))], [])
        );
        next.EnsureSuccessStatusCode();
    }

    /// <summary>The army's boats with no order yet in the open turn.</summary>
    private static async Task<List<Guid>> FreeBoatsAsync(CampaignScenario scenario)
    {
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        var units = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<ArmyUnitResponse>>($"/api/campaigns/{scenario.CampaignId}/units");
        return
        [
            .. units!
                .Where(u => u.Type == UnitType.Boat && turn.Orders.All(o => o.UnitId != u.Id))
                .Select(u => u.Id),
        ];
    }

    private static async Task<List<UnitPosition>> OpenOrdersAsync(CampaignScenario scenario)
    {
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        return [.. turn.Orders];
    }

    [Fact]
    public async Task GiveOrder_Embark_TiesAsManyOfTheArmysFreeBoatsInTheHexAsItsPointsNeed()
    {
        var (scenario, boats) = await AfloatAsync(boats: 3);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);

        var order = await given.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal((OrderKind.Embark, Here), (order!.Kind, new Hex(order.Q, order.R)));
        // 20 points, 14 a boat: two, by name.
        Assert.Equal(boats[..2], order.Boats);
        var tied = (await OpenOrdersAsync(scenario)).Where(o => o.CarriedBy == scenario.UnitId);
        Assert.Equal(
            boats[..2].Select(b => (b, OrderKind.Hold, Here)),
            tied.OrderBy(o => o.UnitId).Select(o => (o.UnitId, o.Kind, new Hex(o.Q, o.R)))
        );
    }

    [Fact]
    public async Task GiveOrder_EmbarkWithTooFewBoats_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync(boats: 1);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_EmbarkOnBoatsAnotherUnitBoarded_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync(boats: 2);
        var other = await LibrarySteps.AddUnitAsync(scenario, "2nd Division", points: 10);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var first = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);
        first.EnsureSuccessStatusCode();

        // Added since the start, it has no position: placed beside the first.
        using var placed = await TurnSteps.PlaceAsync(scenario, other);
        using var second = await TurnSteps.OrderAsync(scenario, turn.Id, Embark, other);

        await second.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_ABoatEmbarking_IsAValidationError()
    {
        var (scenario, boats) = await AfloatAsync(boats: 3);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Embark, boats[2]);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_OnBoats_MovesAsBoatsDo_AndTheBoatsGoWithIt()
    {
        var (scenario, boats) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        Hex[] downstream = [new(1, 0), new(2, 0), new(3, 0)];

        // Three hexes: more than infantry's two over flat ground, a boat's four downstream.
        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(downstream));

        var order = await given.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal(new Hex(3, 0), new Hex(order!.Q, order.R));
        Assert.Equal(boats, order.Boats);
        Assert.All(
            (await OpenOrdersAsync(scenario)).Where(o => o.CarriedBy == scenario.UnitId),
            o => Assert.Equal((OrderKind.Move, new Hex(3, 0)), (o.Kind, new Hex(o.Q, o.R)))
        );
    }

    [Fact]
    public async Task GiveOrder_OnBoatsOverLand_IsAValidationError()
    {
        var (scenario, _) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(South));

        await given.AssertValidationProblemAsync("path");
    }

    [Theory]
    [InlineData(true, false, "forceMarch")]
    [InlineData(false, true, "livesOffTheLand")]
    public async Task GiveOrder_OnBoats_NoForceMarchNorLivingOffTheLand(
        bool forceMarch,
        bool livesOffTheLand,
        string field
    )
    {
        var (scenario, _) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Move, [new Hex(1, 0)], forceMarch, livesOffTheLand)
        );

        await given.AssertValidationProblemAsync(field);
    }

    [Fact]
    public async Task GiveOrder_ToABoatTiedToAUnit_Returns409()
    {
        var (scenario, boats) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold, boats[0]);

        await given.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_DisembarkAcrossARiverSide_LandsThere_AndFreesTheBoatsWhereTheyWere()
    {
        var (scenario, boats) = await AboardAsync();

        await TurnAsync(scenario, Disembark(North));

        Assert.Equal(North, await TurnSteps.HereAsync(scenario));
        // The boats take orders of their own again, where the unit left them.
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var boat = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold, boats[0]);
        var order = await boat.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal((Here, null), (new Hex(order!.Q, order.R), order.CarriedBy));
    }

    [Fact]
    public async Task GiveOrder_DisembarkWhereNoRiverSideIs_IsAValidationError()
    {
        var (scenario, _) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Disembark(South));

        await given.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_DisembarkWhenAshore_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Disembark());

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task UndoOrder_OfAUnitEmbarking_TakesBackItsBoatsOrdersToo()
    {
        var (scenario, _) = await AfloatAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);

        using var undone = await scenario
            .As(Role.Commander)
            .DeleteAsync(
                new Uri($"/api/army-turns/{turn.Id}/orders/{scenario.UnitId}", UriKind.Relative),
                Token
            );

        undone.EnsureSuccessStatusCode();
        Assert.Empty(await OpenOrdersAsync(scenario));
    }

    [Fact]
    public async Task SubmitTurn_WithTheUnitOrdered_NeedsNoOrdersForItsBoats()
    {
        var (scenario, _) = await AboardAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold);

        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);

        submitted.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ListMarches_MovingOnBoats_IsRest()
    {
        var (scenario, _) = await AboardAsync();

        foreach (var to in new[] { new Hex(1, 0), new Hex(2, 0), new Hex(3, 0) })
        {
            await TurnAsync(scenario, TurnSteps.Move(to));
        }

        var marches = await scenario
            .As(Role.Commander)
            .GetAsAsync<List<UnitMarchResponse>>($"/api/armies/{scenario.ArmyId}/marches");
        Assert.All(marches!, m => Assert.Equal((0, 0), (m.MovesInRow, m.ForcedMarchTurns)));
    }

    [Fact]
    public async Task StartNextTurn_OnBoatsInATown_TakesNothing()
    {
        var (scenario, _) = await AboardAsync();
        using var town = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/grid/cells/1/0", UriKind.Relative),
                new UpdateHexCellRequest(
                    Terrain.Flat,
                    false,
                    new HexSettlement(
                        SettlementSize.Town,
                        false,
                        false,
                        CapitalStatus.None,
                        "Wavre"
                    )
                ),
                Token
            );
        town.EnsureSuccessStatusCode();

        await TurnAsync(scenario, TurnSteps.Move(new Hex(1, 0)));

        var score = await scenario
            .As(Role.Umpire)
            .GetAsAsync<ScoreboardResponse>($"/api/campaigns/{scenario.CampaignId}/scoreboard");
        Assert.Null(Assert.Single(score!.Settlements).ArmyId);
    }

    [Fact]
    public async Task GiveOrder_ToABoatWhoseUnitHasNoPointsLeft_IsItsOwn()
    {
        var (scenario, boats) = await AboardAsync();
        using var lost = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/army-units/{scenario.UnitId}", UriKind.Relative),
                new UpdateArmyUnitRequest("1st Division", UnitType.LineInfantry, 5, 0),
                Token
            );
        lost.EnsureSuccessStatusCode();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold, boats[0]);

        given.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GetBoatSettings_ANewCampaign_CarriesTheRules14Points()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var settings = await scenario
            .As(Role.Commander)
            .GetAsAsync<BoatSettingsResponse>(
                $"/api/campaigns/{scenario.CampaignId}/boat-settings"
            );

        Assert.Equal(14, settings!.Capacity);
    }

    [Fact]
    public async Task UpdateBoatSettings_BiggerBoats_NeedFewerOfThem()
    {
        var (scenario, boats) = await AfloatAsync(boats: 2);
        using var set = await SetCapacityAsync(scenario, 20);
        set.EnsureSuccessStatusCode();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);

        Assert.Equal([boats[0]], (await given.Content.ReadAsAsync<UnitPosition>())!.Boats);
    }

    [Fact]
    public async Task UpdateBoatSettings_NoCapacity_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var set = await SetCapacityAsync(scenario, 0);

        await set.AssertValidationProblemAsync("capacity");
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateBoatSettings_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var set = await SetCapacityAsync(scenario, 10, role);

        Assert.Equal(expected, set.StatusCode);
    }

    private static Task<HttpResponseMessage> SetCapacityAsync(
        CampaignScenario scenario,
        int capacity,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/boat-settings", UriKind.Relative),
                new UpdateBoatSettingsRequest(capacity),
                Token
            );
}
