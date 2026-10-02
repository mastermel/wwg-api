using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Boats;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Sightings;
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

    /// <summary>
    /// The river, the unit and <paramref name="boats"/> boats at (0, 0), anything else
    /// <paramref name="beforeStart"/> sets up, and turn 1 open.
    /// </summary>
    private async Task<(CampaignScenario Scenario, List<Guid> Boats)> AfloatAsync(
        int boats = 2,
        Func<CampaignScenario, Task>? beforeStart = null
    )
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

        if (beforeStart is not null)
        {
            await beforeStart(scenario);
        }

        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return (scenario, ids);
    }

    /// <summary>A town at the hex (by default, the unit's).</summary>
    private static async Task TownAsync(CampaignScenario scenario, Hex? at = null)
    {
        var hex = at ?? Here;
        using var town = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/cells/{hex.Q}/{hex.R}",
                    UriKind.Relative
                ),
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
    }

    /// <summary>An army of the other side, with no commander, and a brigade at the hex.</summary>
    private static async Task<Guid> EnemyAsync(CampaignScenario scenario, Hex at)
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Prussians", null, scenario.OtherSideId),
                Token
            );
        var enemy = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var unit = await LibrarySteps.AddUnitAsync(scenario, "Brigade", armyId: enemy);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, at);
        placed.EnsureSuccessStatusCode();
        return enemy;
    }

    private static Task<HttpResponseMessage> BuildAsync(CampaignScenario scenario, Guid turnId) =>
        TurnSteps.OrderAsync(scenario, turnId, new GiveOrderRequest(OrderKind.BuildBoat, null));

    private static async Task<List<ArmyUnitResponse>> BoatsOfAsync(CampaignScenario scenario) =>
        [
            .. (
                await scenario
                    .As(Role.Umpire)
                    .GetAsAsync<List<ArmyUnitResponse>>(
                        $"/api/campaigns/{scenario.CampaignId}/units"
                    )
            )!.Where(u => u.Type == UnitType.Boat),
        ];

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
    public async Task StartNextTurn_LooseBoatsInATown_TakeNothing()
    {
        var (scenario, boats) = await AfloatAsync(
            boats: 1,
            beforeStart: s => TownAsync(s, new Hex(1, 0))
        );
        // The unit holds; its boat rows down the river into the town.
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var rowed = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            TurnSteps.Move(new Hex(1, 0)),
            boats[0]
        );
        rowed.EnsureSuccessStatusCode();

        await TurnAsync(scenario, TurnSteps.Hold);

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
    public async Task StartNextTurn_TheSecondTurnOfWorkInARiverTown_BuildsABoatThere()
    {
        var (scenario, _) = await AfloatAsync(boats: 0, beforeStart: s => TownAsync(s));
        var build = new GiveOrderRequest(OrderKind.BuildBoat, null);

        await TurnAsync(scenario, build);
        Assert.Empty(await BoatsOfAsync(scenario));
        await TurnAsync(scenario, build);

        var boat = Assert.Single(await BoatsOfAsync(scenario));
        Assert.Equal(
            ("Boat 1", (Guid?)null, scenario.ArmyId),
            (boat.Name, boat.UnitId, boat.ArmyId)
        );
        var positions = await scenario
            .As(Role.Commander)
            .GetAsAsync<List<UnitPosition>>($"/api/campaigns/{scenario.CampaignId}/positions");
        var at = positions!.Single(p => p.UnitId == boat.Id);
        Assert.Equal(Here, new Hex(at.Q, at.R));
    }

    [Fact]
    public async Task StartNextTurn_WorkInterrupted_BuildsNothing()
    {
        var (scenario, _) = await AfloatAsync(boats: 0, beforeStart: s => TownAsync(s));
        var build = new GiveOrderRequest(OrderKind.BuildBoat, null);

        await TurnAsync(scenario, build);
        await TurnAsync(scenario, TurnSteps.Hold);
        await TurnAsync(scenario, build);

        Assert.Empty(await BoatsOfAsync(scenario));
    }

    [Fact]
    public async Task GiveOrder_BuildBoatOutsideASettlement_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync(boats: 0);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await BuildAsync(scenario, turn.Id);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_BuildBoatInATownWithNoWaterway_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync(boats: 0, beforeStart: s => TownAsync(s, South));
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        await TurnAsync(scenario, TurnSteps.Move(South));
        turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await BuildAsync(scenario, turn.Id);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_BuildBoatWithTheEnemyInTheTown_IsAValidationError()
    {
        var (scenario, _) = await AfloatAsync(
            boats: 0,
            beforeStart: async s =>
            {
                await TownAsync(s);
                await EnemyAsync(s, Here);
            }
        );
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await BuildAsync(scenario, turn.Id);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task GiveOrder_BuildBoatOnBoats_IsAValidationError()
    {
        var (scenario, _) = await AboardAsync();
        await TownAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var given = await BuildAsync(scenario, turn.Id);

        await given.AssertValidationProblemAsync("kind");
    }

    [Fact]
    public async Task StartNextTurn_ASightingShowingTheBoats_SaysTheForceWasAfloat()
    {
        var enemy = Guid.Empty;
        var (scenario, _) = await AfloatAsync(beforeStart: async s =>
            enemy = await EnemyAsync(s, new Hex(2, 0))
        );
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var embarked = await TurnSteps.OrderAsync(scenario, turn.Id, Embark);
        embarked.EnsureSuccessStatusCode();
        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);
        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);
        approved.EnsureSuccessStatusCode();
        await HoldEnemyAsync(scenario, enemy);

        using var next = await TurnSteps.StartNextTurnAsync(
            scenario,
            request: new(
                [],
                [
                    new SightingRequest(
                        enemy,
                        Here.Q,
                        Here.R,
                        true,
                        false,
                        false,
                        SightingStrength.Hidden,
                        ShowsAfloat: true
                    ),
                ]
            )
        );

        next.EnsureSuccessStatusCode();
        var seen = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<SightingResponse>>($"/api/campaigns/{scenario.CampaignId}/sightings");
        Assert.True(Assert.Single(seen!).Afloat);
    }

    /// <summary>The enemy's units hold, submitted and approved by the Umpire.</summary>
    private static async Task HoldEnemyAsync(CampaignScenario scenario, Guid enemy)
    {
        var umpire = scenario.As(Role.Umpire);
        var turn = (
            await umpire.GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{enemy}/turns")
        )!.Single(t => t.Open);
        var units = await umpire.GetAsAsync<List<ArmyUnitResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/units"
        );
        foreach (var unit in units!.Where(u => u.ArmyId == enemy))
        {
            using var held = await TurnSteps.OrderAsync(
                scenario,
                turn.Id,
                TurnSteps.Hold,
                unit.Id,
                Role.Umpire
            );
            held.EnsureSuccessStatusCode();
        }
        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Umpire);
        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);
        approved.EnsureSuccessStatusCode();
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
