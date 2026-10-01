using System.Net;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Forced marches (step 47, decision 0018): force-march orders, and each unit's count.</summary>
public sealed class MarchTests : ApiTest
{
    /// <summary>East from the start, inside the Waterloo grid: flat, half a turn a hex for infantry.</summary>
    private static readonly Hex[] East = [new(1, 0), new(2, -1), new(3, -1)];

    private static GiveOrderRequest ForceMarch(params Hex[] path) => TurnSteps.ForceMarch(path);

    /// <summary>Started, turn 1 a Morning, and no nation marching further or less.</summary>
    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    private static async Task<UnitMarchResponse> MarchAsync(
        CampaignScenario scenario,
        Role role = Role.Commander
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<UnitMarchResponse>>($"/api/armies/{scenario.ArmyId}/marches")
        )!.Single(m => m.UnitId == scenario.UnitId);

    [Fact]
    public async Task ListMarches_NothingPlayed_IsRested()
    {
        using var scenario = await StartedAsync();

        var march = await MarchAsync(scenario);

        Assert.Equal(new UnitMarchResponse(scenario.UnitId, 0, 0, 0, 0, 0, 0), march);
    }

    [Fact]
    public async Task ListMarches_ThreeMovesInARow_IsTheFirstForcedMarch_AndFree()
    {
        using var scenario = await StartedAsync();

        await TurnSteps.PlayAsync(scenario, "move", "move");
        var two = await MarchAsync(scenario);
        await TurnSteps.PlayAsync(scenario, "move");
        var three = await MarchAsync(scenario);

        Assert.Equal((2, 0, 0), (two.MovesInRow, two.ForcedMarchTurns, two.MoveCosts));
        // The fourth move would be a second turn of forced march: normal attrition.
        Assert.Equal((1, 1), (three.ForcedMarchTurns, three.MoveCosts));
    }

    [Fact]
    public async Task ListMarches_MovingOn_DoublesTheCostEachTurn()
    {
        using var scenario = await StartedAsync();

        await TurnSteps.PlayAsync(scenario, "move", "move", "move", "move");
        var four = await MarchAsync(scenario);
        await TurnSteps.PlayAsync(scenario, "move");
        var five = await MarchAsync(scenario);

        Assert.Equal((2, 2), (four.ForcedMarchTurns, four.MoveCosts));
        Assert.Equal((3, 4), (five.ForcedMarchTurns, five.MoveCosts));
    }

    [Fact]
    public async Task ListMarches_TwoForceMarches_AreTheFirstForcedMarch()
    {
        using var scenario = await StartedAsync();

        // Turns 1 and 2: Morning and Afternoon.
        await TurnSteps.PlayAsync(scenario, "force");
        var one = await MarchAsync(scenario);
        await TurnSteps.PlayAsync(scenario, "force");
        var two = await MarchAsync(scenario);

        Assert.Equal((1, 1, 0), (one.MovesInRow, one.ForceMarchesInRow, one.ForcedMarchTurns));
        Assert.Equal(0, one.ForceMarchCosts);
        Assert.Equal((1, 1), (two.ForcedMarchTurns, two.MoveCosts));
    }

    [Fact]
    public async Task ListMarches_AHold_RestsOneTurnOff()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.PlayAsync(scenario, "move", "move", "move", "move");

        await TurnSteps.PlayAsync(scenario, "hold");
        var rested = await MarchAsync(scenario);
        await TurnSteps.PlayAsync(scenario, "hold");
        var fully = await MarchAsync(scenario);

        // Rested one of two: moving now carries the run on, at normal attrition.
        Assert.Equal((1, 1), (rested.ForcedMarchTurns, rested.MoveCosts));
        Assert.Equal(new UnitMarchResponse(scenario.UnitId, 0, 0, 0, 0, 0, 0), fully);
    }

    [Fact]
    public async Task ListMarches_AHoldBeforeTheFirstForcedMarch_EndsTheRun()
    {
        using var scenario = await StartedAsync();

        await TurnSteps.PlayAsync(scenario, "move", "move", "hold", "move");

        Assert.Equal(
            (1, 0),
            ((await MarchAsync(scenario)).MovesInRow, (await MarchAsync(scenario)).ForcedMarchTurns)
        );
    }

    [Fact]
    public async Task ListMarches_TheOrderAsGiven_SaysWhatItCosts()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.PlayAsync(scenario, "move", "move", "move", "move");
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var moved = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(East[0]));
        var moving = await MarchAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold);
        var holding = await MarchAsync(scenario);

        Assert.Equal(2, moving.OrderCosts);
        Assert.Equal(0, holding.OrderCosts);
    }

    [Fact]
    public async Task GiveOrder_AForceMarch_GoesAFlatHexFurther()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var marching = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(East));
        using var forced = await TurnSteps.OrderAsync(scenario, turn.Id, ForceMarch(East));
        var order = (await TurnSteps.OpenArmyTurnAsync(scenario)).Orders.Single();

        await marching.AssertValidationProblemAsync("path");
        Assert.Equal(HttpStatusCode.OK, forced.StatusCode);
        Assert.True(order.ForceMarch);
    }

    [Fact]
    public async Task GiveOrder_AForceMarchByNight_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario, TurnPart.Night);
        await TurnSteps.StartedAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, ForceMarch(East[0]));

        await response.AssertValidationProblemAsync("forceMarch");
    }

    [Fact]
    public async Task GiveOrder_AHoldMarkedAsAForceMarch_IsAHold()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            new GiveOrderRequest(OrderKind.Hold, null, ForceMarch: true)
        );
        var order = (await TurnSteps.OpenArmyTurnAsync(scenario)).Orders.Single();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(order.ForceMarch);
    }

    [Fact]
    public async Task GiveOrder_TheUmpiresForceMarch_IsInTheTurnsHistory()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            ForceMarch(East[0]),
            role: Role.Umpire
        );
        var history = (await TurnSteps.OpenArmyTurnAsync(scenario)).History.Single();

        Assert.Equal("Set to force march.", history.UnitNotes.Single().Text);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListMarches_ByRole_FollowsTheArmysMoves(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/marches", UriKind.Relative),
                TestContext.Current.CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
