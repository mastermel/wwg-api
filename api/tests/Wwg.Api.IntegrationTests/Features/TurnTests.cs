using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Setting up (turn 0), placing units, starting the campaign, and who sees where units are
/// (DESIGN.md §5.1, §5.2's visibility rule).
/// </summary>
public sealed class TurnTests : ApiTest
{
    private static Task<CampaignTurnsResponse?> TurnsAsync(CampaignScenario scenario, Role role) =>
        scenario
            .As(role)
            .GetAsAsync<CampaignTurnsResponse>($"/api/campaigns/{scenario.CampaignId}/turns");

    private static Task<List<UnitPosition>?> PositionsAsync(
        CampaignScenario scenario,
        Role role,
        int? turn = null
    ) =>
        scenario
            .As(role)
            .GetAsAsync<List<UnitPosition>>(
                $"/api/campaigns/{scenario.CampaignId}/positions{(turn is { } t ? $"?turn={t}" : "")}"
            );

    /// <summary>The scenario, with its area set and its unit placed: ready to start.</summary>
    private async Task<CampaignScenario> ReadyAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.ReadyAsync(scenario);
        return scenario;
    }

    [Fact]
    public async Task ListTurns_NewCampaign_IsSettingUpAndSaysWhatStopsItStarting()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var turns = await TurnsAsync(scenario, Role.Umpire);

        Assert.Equal((CampaignStage.Setup, 0), (turns?.Stage, turns?.OpenTurn));
        Assert.Equal(["Choose the map's area.", "Place 1 unit on the map."], turns!.StartProblems);
    }

    [Fact]
    public async Task ListTurns_ForAPlayer_LeavesOutWhatStopsItStarting()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var turns = await TurnsAsync(scenario, Role.Commander);

        Assert.Empty(turns!.StartProblems);
    }

    [Fact]
    public async Task PlaceUnit_NoAreaYet_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await TurnSteps.PlaceAsync(scenario, scenario.UnitId);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PlaceUnit_OutsideTheArea_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        // Far to the north: no hex of the Waterloo grid.
        using var response = await TurnSteps.PlaceAsync(scenario, scenario.UnitId, new Hex(0, -10));

        await response.AssertValidationProblemAsync("q");
    }

    [Fact]
    public async Task PlaceUnit_WhileSettingUp_IsItsTurnZeroPosition_ForTheUmpireAndItsCommander()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var response = await TurnSteps.PlaceAsync(scenario, scenario.UnitId, new Hex(1, -1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var role in new[] { Role.Admin, Role.Umpire, Role.Commander })
        {
            var position = Assert.Single((await PositionsAsync(scenario, role, turn: 0))!);
            Assert.Equal(
                (scenario.UnitId, 0, OrderKind.Move, 1, -1),
                (position.UnitId, position.Turn, position.Kind, position.Q, position.R)
            );
        }

        // Not the other Player: it isn't their army.
        Assert.Empty((await PositionsAsync(scenario, Role.Player, turn: 0))!);
    }

    [Fact]
    public async Task PlaceUnit_InAHex_IsAtThatHexsCentre()
    {
        var waterloo = HexGridFigures.Cases[0];
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario, waterloo.HexSize);
        var checkedHexes = 0;

        foreach (var point in waterloo.Points)
        {
            using var response = await TurnSteps.PlaceAsync(
                scenario,
                scenario.UnitId,
                new Hex(point.Q, point.R)
            );
            // Hexes outside the grid are refused (PlaceUnit_OutsideTheArea_IsAValidationError).
            if (!response.IsSuccessStatusCode)
            {
                continue;
            }

            var position = await response.Content.ReadAsAsync<UnitPosition>();
            Assert.Equal(point.Centre.Latitude, position!.Latitude, 6);
            Assert.Equal(point.Centre.Longitude, position.Longitude, 6);
            checkedHexes++;
        }

        Assert.True(checkedHexes >= 4, $"Only {checkedHexes} of the figures' hexes were checked.");
    }

    [Fact]
    public async Task PlaceUnit_Again_MovesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);
        using var first = await TurnSteps.PlaceAsync(scenario, scenario.UnitId);

        using var second = await TurnSteps.PlaceAsync(scenario, scenario.UnitId, new Hex(2, -1));

        var position = Assert.Single((await PositionsAsync(scenario, Role.Umpire, turn: 0))!);
        Assert.Equal((2, -1), (position.Q, position.R));
    }

    [Fact]
    public async Task UnplaceUnit_WhileSettingUp_TakesItOffTheMap()
    {
        using var scenario = await ReadyAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/army-units/{scenario.UnitId}/placement", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await PositionsAsync(scenario, Role.Umpire, turn: 0))!);
    }

    [Fact]
    public async Task StartCampaign_Ready_CompletesTurnZeroAndOpensTurnOne()
    {
        using var scenario = await ReadyAsync();

        using var response = await TurnSteps.StartAsync(scenario);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var turns = await TurnsAsync(scenario, Role.Umpire);
        Assert.Equal((CampaignStage.Running, 1), (turns?.Stage, turns?.OpenTurn));
        Assert.Equal(
            [(0, ArmyTurnStatus.Completed, true), (1, ArmyTurnStatus.Draft, false)],
            turns!.Turns.Select(t =>
                (t.Number, Assert.Single(t.ArmyTurns).Status, t.ClosedAt is not null)
            )
        );
        Assert.Equal(["First Corps hasn't submitted yet."], turns.StartProblems);
    }

    [Fact]
    public async Task StartCampaign_Started_PlacementsAreWhereUnitsAreNow()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);

        var now = Assert.Single((await PositionsAsync(scenario, Role.Commander))!);

        Assert.Equal(
            (scenario.UnitId, 0, ArmyTurnStatus.Completed),
            (now.UnitId, now.Turn, now.Status)
        );
        Assert.Empty((await PositionsAsync(scenario, Role.Player))!);
    }

    [Fact]
    public async Task StartCampaign_AUnitNotPlaced_Returns409SayingSo()
    {
        using var scenario = await ReadyAsync();
        using var reserve = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Reserve", null, scenario.SideId),
                CancellationToken
            );
        var reserveId = (await reserve.Content.ReadAsAsync<ArmyResponse>())!.Id;
        await LibrarySteps.AddUnitAsync(scenario, "Guard", armyId: reserveId);

        using var response = await TurnSteps.StartAsync(scenario);

        var problem = await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Equal("Place 1 unit on the map.", problem.Detail, StringComparer.Ordinal);
    }

    [Fact]
    public async Task StartCampaign_Twice_Returns409()
    {
        using var scenario = await ReadyAsync();
        using var first = await TurnSteps.StartAsync(scenario);

        using var second = await TurnSteps.StartAsync(scenario);

        await second.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PlaceUnit_AfterTheStart_AUnitAlreadyPlaced_Returns409()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);

        using var response = await TurnSteps.PlaceAsync(scenario, scenario.UnitId, new Hex(2, -1));

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PlaceUnit_AfterTheStart_AUnitAddedSince_IsPlacedWhereItsArmyLastWas()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);
        var hussars = await LibrarySteps.AddUnitAsync(scenario, "Hussars", UnitType.LightCavalry);

        using var response = await TurnSteps.PlaceAsync(scenario, hussars, new Hex(2, -1));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var now = (await PositionsAsync(scenario, Role.Commander))!;
        Assert.Equal(
            [(hussars, 0), (scenario.UnitId, 0)],
            now.Select(p => (p.UnitId, p.Turn)).OrderBy(p => p.UnitId == scenario.UnitId)
        );
    }

    [Fact]
    public async Task UnplaceUnit_AfterTheStart_Returns409()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/army-units/{scenario.UnitId}/placement", UriKind.Relative),
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeletingACampaign_WithTurns_DeletesThemToo()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            (0, 0, 0),
            (
                await WithDbAsync(db => db.CampaignTurns.CountAsync(CancellationToken)),
                await WithDbAsync(db => db.ArmyTurns.CountAsync(CancellationToken)),
                await WithDbAsync(db => db.UnitOrders.CountAsync(CancellationToken))
            )
        );
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListTurnsAndPositions_ByRole_ReturnExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        foreach (var path in new[] { "turns", "positions" })
        {
            using var response = await scenario
                .As(role)
                .GetAsync(
                    new Uri($"/api/campaigns/{scenario.CampaignId}/{path}", UriKind.Relative),
                    CancellationToken
                );
            Assert.Equal(expected, response.StatusCode);
        }
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task PlaceUnit_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario);

        using var response = await TurnSteps.PlaceAsync(scenario, scenario.UnitId, role: role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task StartCampaign_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await ReadyAsync();

        using var response = await TurnSteps.StartAsync(scenario, role);

        Assert.Equal(expected, response.StatusCode);
    }

    // The visibility rule, once started: where the scenario's army's unit is.
    [Theory]
    [InlineData(Role.Admin, true)]
    [InlineData(Role.Umpire, true)]
    [InlineData(Role.Commander, true)]
    [InlineData(Role.Player, false)]
    public async Task ListPositions_ByRole_OnlyThoseWhoMaySeeTheArmy(Role role, bool sees)
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);

        var positions = await PositionsAsync(scenario, role);

        Assert.Equal(sees, positions!.Any(p => p.UnitId == scenario.UnitId));
    }

    [Theory]
    [InlineData("armies")]
    [InlineData("army-units")]
    public async Task Delete_AfterTheStart_Returns409AndKeepsIt(string what)
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);
        var id = string.Equals(what, "armies", StringComparison.Ordinal)
            ? scenario.ArmyId
            : scenario.UnitId;

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(new Uri($"/api/{what}/{id}", UriKind.Relative), CancellationToken);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        Assert.Single((await PositionsAsync(scenario, Role.Umpire))!);
    }

    [Theory]
    [InlineData("armies")]
    [InlineData("army-units")]
    public async Task Delete_WhileSettingUp_ItsPlacementGoesToo(string what)
    {
        using var scenario = await ReadyAsync();
        var id = string.Equals(what, "armies", StringComparison.Ordinal)
            ? scenario.ArmyId
            : scenario.UnitId;

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(new Uri($"/api/{what}/{id}", UriKind.Relative), CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await PositionsAsync(scenario, Role.Umpire, turn: 0))!);
    }

    [Fact]
    public async Task CreateArmy_AfterTheStart_JoinsTheTurnsAndItsUnitsCanBePlaced()
    {
        using var scenario = await ReadyAsync();
        using var started = await TurnSteps.StartAsync(scenario);
        using var reserve = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Reserve", null, scenario.SideId),
                CancellationToken
            );
        var reserveId = (await reserve.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var guard = await LibrarySteps.AddUnitAsync(scenario, "Guard", armyId: reserveId);

        using var placed = await TurnSteps.PlaceAsync(scenario, guard, new Hex(-2, 2));

        Assert.Equal(HttpStatusCode.OK, placed.StatusCode);
        var turns = (await TurnsAsync(scenario, Role.Umpire))!.Turns;
        Assert.Equal(
            [ArmyTurnStatus.Completed, ArmyTurnStatus.Draft],
            turns.Select(t => t.ArmyTurns.Single(a => a.ArmyId == reserveId).Status)
        );
        Assert.Contains((await PositionsAsync(scenario, Role.Umpire))!, p => p.UnitId == guard);
    }
}
