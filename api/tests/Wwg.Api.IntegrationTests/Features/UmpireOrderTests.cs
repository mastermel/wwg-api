using System.Net;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The Umpire giving orders on a commander's behalf (decision 0011).</summary>
public sealed class UmpireOrderTests : ApiTest
{
    private static readonly GiveOrderRequest Hold = new(OrderKind.Hold, null, null);

    private static GiveOrderRequest MoveTo(double latitude) => new(OrderKind.Move, latitude, 4.4);

    /// <summary>Started, with every unit type able to move 5 km a turn.</summary>
    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario, limitMetres: 5_000);
        return scenario;
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_IsMarkedAndRecordedInTheHistory()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        response.EnsureSuccessStatusCode();
        var after = await TurnSteps.OpenArmyTurnAsync(scenario);
        Assert.True(Assert.Single(after.Orders).ByUmpire);
        var edited = Assert.Single(after.History);
        Assert.Equal((ArmyTurnEventKind.Edited, "Test User"), (edited.Kind, edited.ByName));
        Assert.Equal([new UnitNoteDto(scenario.UnitId, "Set to hold.")], edited.UnitNotes);
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpireAgain_AddsToTheSameEdit()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        using var moved = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            MoveTo(50.72),
            role: Role.Umpire
        );

        moved.EnsureSuccessStatusCode();
        var edited = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).History);
        Assert.Equal([new UnitNoteDto(scenario.UnitId, "Set to move.")], edited.UnitNotes);
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_PastTheLimit_Saves()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        // About 8.9 km: past the 5 km limit, but inside the area.
        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            MoveTo(50.78),
            role: Role.Umpire
        );

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_OutsideTheArea_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            MoveTo(50.85),
            role: Role.Umpire
        );

        await response.AssertValidationProblemAsync("latitude");
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_ToASubmittedTurn_SavesAndLeavesItSubmitted()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            MoveTo(50.72),
            role: Role.Umpire
        );

        var order = await response.Content.ReadAsAsync<UnitPosition>();
        Assert.Equal((ArmyTurnStatus.Submitted, true), (order?.Status, order?.ByUmpire));
        Assert.Equal(
            ArmyTurnStatus.Submitted,
            (await TurnSteps.OpenArmyTurnAsync(scenario)).Status
        );
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_ToAnApprovedTurn_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.CompletedAsync(scenario);

        using var response = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_ByTheCommanderAfterTheUmpire_IsTheirsAgain()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        using var moved = await TurnSteps.OrderAsync(scenario, turn.Id, MoveTo(50.72));

        moved.EnsureSuccessStatusCode();
        Assert.False(Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders).ByUmpire);
    }

    [Fact]
    public async Task UndoOrder_ByTheUmpire_IsRecorded_AndApprovingThenNeedsAnOrder()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var undone = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/army-turns/{turn.Id}/orders/{scenario.UnitId}", UriKind.Relative),
                TestContext.Current.CancellationToken
            );
        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);

        Assert.Equal(HttpStatusCode.NoContent, undone.StatusCode);
        await approved.AssertProblemAsync(HttpStatusCode.Conflict);
        var edited = (await TurnSteps.OpenArmyTurnAsync(scenario)).History[^1];
        Assert.Equal(
            (ArmyTurnEventKind.Edited, "Order taken back."),
            (edited.Kind, Assert.Single(edited.UnitNotes).Text)
        );
    }

    [Fact]
    public async Task SubmitTurn_ByTheUmpire_EmailsTheCommanderWithTheOrdersTheUmpireSet()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Umpire);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var email = await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Equal("The Peninsular War: turn 1 submitted for First Corps", email.Subject);
        Assert.Contains(
            "Orders the Umpire set:\n- 1st Division: hold",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SubmitTurn_ByTheUmpire_ForAnArmyWithNoCommander_Submits()
    {
        using var scenario = await StartedAsync();
        await WithDbAsync(db =>
            db.Armies.Where(a => a.Id == scenario.ArmyId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(a => a.CommanderId, (Guid?)null),
                    TestContext.Current.CancellationToken
                )
        );
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, Hold, role: Role.Umpire);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Umpire);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(
            ArmyTurnStatus.Submitted,
            (await TurnSteps.OpenArmyTurnAsync(scenario)).Status
        );
    }

    [Fact]
    public async Task ApproveTurn_AfterTheUmpireChangedAnOrder_ListsItInTheEmail()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);
        using var moved = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            MoveTo(50.72),
            role: Role.Umpire
        );

        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);

        Assert.Equal(HttpStatusCode.NoContent, approved.StatusCode);
        var email = await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Contains("- 1st Division: move", email.TextBody, StringComparison.Ordinal);
    }
}
