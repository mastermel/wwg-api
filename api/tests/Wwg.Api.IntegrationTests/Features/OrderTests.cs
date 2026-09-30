using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.Features.Units;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// A commander's orders for their army's turn: Move and Hold, their limits, and undo
/// (DESIGN.md §3.13, §5.2).
/// </summary>
public sealed class OrderTests : ApiTest
{
    /// <summary>
    /// Started: the unit (line infantry, so two flat hexes a turn) in hex (0, 0) of the Waterloo
    /// grid, whose hexes run from (0, -2) in the north to (0, 2) in the south.
    /// </summary>
    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    private static readonly GiveOrderRequest Hold = TurnSteps.Hold;

    private static GiveOrderRequest Move(params Hex[] path) => TurnSteps.Move(path);

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task GiveOrder_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.NoContent)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UndoOrder_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await scenario
            .As(role)
            .DeleteAsync(
                new Uri($"/api/army-turns/{turn.Id}/orders/{scenario.UnitId}", UriKind.Relative),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_MoveWithinTheLimit_SavesTheDraftMoveAndItsPath()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            Move(new Hex(0, -1), new Hex(0, -2))
        );

        response.EnsureSuccessStatusCode();
        var order = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
        Assert.Equal(
            (OrderKind.Move, ArmyTurnStatus.Draft, 1, 0, -2),
            (order.Kind, order.Status, order.Turn, order.Q, order.R)
        );
        Assert.Equal([new Hex(0, -1), new Hex(0, -2)], order.Path);
        // The hex's centre, 2 hexes (about 9.7 km) north of the area's middle.
        Assert.InRange(order.Latitude, 50.78, 50.79);
    }

    [Fact]
    public async Task GiveOrder_AgainForTheSameUnit_ReplacesTheOrder()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var first = await TurnSteps.OrderAsync(scenario, turn.Id, Move(new Hex(0, -1)));

        using var second = await TurnSteps.OrderAsync(scenario, turn.Id, Move(new Hex(1, 0)));

        second.EnsureSuccessStatusCode();
        var order = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
        Assert.Equal((1, 0), (order.Q, order.R));
    }

    [Fact]
    public async Task GiveOrder_MoveTooFar_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // Three hexes: infantry moves two on flat ground.
        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            Move(new Hex(1, -1), new Hex(2, -2), new Hex(3, -3))
        );

        await response.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_MoveOutsideTheArea_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // (0, -3) is north of the area.
        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            Move(new Hex(0, -1), new Hex(0, -2), new Hex(0, -3))
        );

        await response.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_MoveWithoutAPlace_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Move, null)
        );

        await response.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_MoveSkippingAHex_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Move(new Hex(0, -2)));

        await response.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_Hold_StaysWhereTheUnitIs()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // Whatever path the request gives: a Hold ignores it.
        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Hold, [new Hex(0, -1)])
        );

        var order = await response.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal(
            (OrderKind.Hold, TurnSteps.Start.Q, TurnSteps.Start.R, 0),
            (order?.Kind, order?.Q, order?.R, order?.Path.Count)
        );
    }

    [Fact]
    public async Task UndoOrder_AfterAMove_RemovesTheOrder()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var moved = await TurnSteps.OrderAsync(scenario, turn.Id, Move(new Hex(0, -1)));

        using var response = await scenario
            .As(Role.Commander)
            .DeleteAsync(
                new Uri($"/api/army-turns/{turn.Id}/orders/{scenario.UnitId}", UriKind.Relative),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
    }

    [Fact]
    public async Task GiveOrder_ToASubmittedTurn_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        await WithDbAsync(db =>
            db.ArmyTurns.Where(t => t.Id == turn.Id)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(t => t.Status, ArmyTurnStatus.Submitted),
                    TestContext.Current.CancellationToken
                )
        );

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Hold);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_ToTheSetupTurn_Returns409()
    {
        using var scenario = await StartedAsync();
        var setup = (await TurnSteps.ArmyTurnsAsync(scenario)).Single(t => t.Turn == 0);

        using var response = await TurnSteps.OrderAsync(scenario, setup.Id, Hold);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_UnitNotYetPlaced_Returns409()
    {
        using var scenario = await StartedAsync();
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateUnitRequest(
                    "2nd Division",
                    UnitType.LineInfantry,
                    Unit.MinFightingFactor,
                    Unit.MinPoints
                ),
                TestContext.Current.CancellationToken
            );
        var unit = await created.Content.ReadAsAsync<UnitResponse>();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, unit?.Id);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_UnitNotInTheArmy_Returns404()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            Hold,
            Guid.CreateVersion7()
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListArmyTurns_AfterStarting_GivesTheOpenDraftThenTheSetup()
    {
        using var scenario = await StartedAsync();

        var turns = await TurnSteps.ArmyTurnsAsync(scenario);

        Assert.Equal(
            [(1, true, ArmyTurnStatus.Draft), (0, false, ArmyTurnStatus.Completed)],
            turns.Select(t => (t.Turn, t.Open, t.Status))
        );
        var placement = Assert.Single(turns[1].Orders);
        Assert.Equal(scenario.UnitId, placement.UnitId);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListArmyTurns_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/turns", UriKind.Relative),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
