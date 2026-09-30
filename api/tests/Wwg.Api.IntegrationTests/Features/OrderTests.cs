using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
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
    /// <summary>Started, with every unit type able to move 5 km a turn.</summary>
    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario, limitMetres: 5_000);
        return scenario;
    }

    private static GiveOrderRequest MoveTo(double latitude, double longitude = 4.4) =>
        new(OrderKind.Move, latitude, longitude);

    private static readonly GiveOrderRequest Hold = new(OrderKind.Hold, null, null);

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
    public async Task GiveOrder_MoveWithinTheLimit_SavesTheDraftMove()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.73));

        response.EnsureSuccessStatusCode();
        var order = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
        Assert.Equal(
            (OrderKind.Move, ArmyTurnStatus.Draft, 1, 50.73, 4.4),
            (order.Kind, order.Status, order.Turn, order.Latitude, order.Longitude)
        );
    }

    [Fact]
    public async Task GiveOrder_AgainForTheSameUnit_ReplacesTheOrder()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var first = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.73));

        using var second = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.68));

        second.EnsureSuccessStatusCode();
        var order = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
        Assert.Equal(50.68, order.Latitude);
    }

    [Fact]
    public async Task GiveOrder_MoveTooFar_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // 0.05° of latitude is about 5.6 km: over the 5 km limit, but inside the area.
        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.75));

        await response.AssertValidationProblemAsync("latitude");
    }

    [Fact]
    public async Task GiveOrder_MoveOutsideTheArea_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario, limitMetres: 50_000);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.85));

        await response.AssertValidationProblemAsync("latitude");
    }

    [Fact]
    public async Task GiveOrder_MoveWithoutAPlace_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Move, null, null)
        );

        await response.AssertValidationProblemAsync("latitude");
    }

    [Fact]
    public async Task GiveOrder_Hold_StaysWhereTheUnitIs()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // Wherever the request says: a Hold ignores it.
        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Hold, 50.6, 4.2)
        );

        var order = await response.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal(
            (OrderKind.Hold, TurnSteps.Start.Latitude, TurnSteps.Start.Longitude),
            (order?.Kind, order?.Latitude, order?.Longitude)
        );
    }

    [Fact]
    public async Task UndoOrder_AfterAMove_RemovesTheOrder()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var moved = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.73));

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
                    UnitType.HeavyInfantry,
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
