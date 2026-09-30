using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Moving turns along: submit, approve, send back, revert and the next turn, with their history
/// and emails (DESIGN.md §5.1, §5.2).
/// </summary>
public sealed class TurnActionTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private async Task<CampaignScenario> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    /// <summary>Adds a unit to the scenario's army (the Umpire), placed at the start if wanted.</summary>
    private static async Task<Guid> AddUnitAsync(CampaignScenario scenario, bool place)
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateArmyUnitRequest(
                    "2nd Division",
                    UnitType.LightInfantry,
                    ArmyUnit.MinFightingFactor,
                    ArmyUnit.MinPoints
                ),
                Token
            );
        var unit = await created.Content.ReadAsAsync<ArmyUnitResponse>();
        var id = unit?.Id ?? throw new InvalidOperationException("No unit.");
        if (place)
        {
            using var placed = await TurnSteps.PlaceAsync(scenario, id);
            placed.EnsureSuccessStatusCode();
        }

        return id;
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.NoContent)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task SubmitTurn_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Hold);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "submit", role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData("approve", Role.Admin, HttpStatusCode.NoContent)]
    [InlineData("approve", Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData("approve", Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData("approve", Role.Player, HttpStatusCode.Forbidden)]
    [InlineData("approve", Role.NonMember, HttpStatusCode.NotFound)]
    [InlineData("send-back", Role.Admin, HttpStatusCode.NoContent)]
    [InlineData("send-back", Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData("send-back", Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData("send-back", Role.Player, HttpStatusCode.Forbidden)]
    [InlineData("send-back", Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ReviewTurn_ByRole_ReturnsExpectedStatus(
        string action,
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            turn.Id,
            action,
            role,
            new ReviewTurnRequest(null, null)
        );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task RevertTurn_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.CompletedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            turn.Id,
            "revert",
            role,
            new ReviewTurnRequest(null, null)
        );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task StartNextTurn_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();
        await TurnSteps.CompletedAsync(scenario);

        using var response = await TurnSteps.StartNextTurnAsync(scenario, role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task SubmitTurn_EveryUnitOrdered_IsSubmittedAndEmailsTheUmpire()
    {
        using var scenario = await StartedAsync();

        await TurnSteps.SubmittedAsync(scenario);

        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        Assert.Equal(ArmyTurnStatus.Submitted, turn.Status);
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, turn.SubmittedAt);
        var submitted = Assert.Single(turn.History);
        Assert.Equal(
            (ArmyTurnEventKind.Submitted, "Test User"),
            (submitted.Kind, submitted.ByName)
        );
        var email = await Emails.WaitForEmailToAsync("umpire@example.com");
        Assert.Equal("The Peninsular War: First Corps submitted turn 1", email.Subject);
        Assert.Contains(
            $"/campaigns/{scenario.CampaignId}/map",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SubmitTurn_AUnitWithoutAnOrder_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SubmitTurn_AUnitNotYetPlaced_DoesntHoldItUp()
    {
        using var scenario = await StartedAsync();
        await AddUnitAsync(scenario, place: false);

        await TurnSteps.SubmittedAsync(scenario);

        Assert.Equal(
            ArmyTurnStatus.Submitted,
            (await TurnSteps.OpenArmyTurnAsync(scenario)).Status
        );
    }

    [Fact]
    public async Task SubmitTurn_AlreadySubmitted_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_WhileSubmitted_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            TurnSteps.Move(new Hex(0, -1))
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ApproveTurn_Submitted_CompletesItAndEmailsTheCommander()
    {
        using var scenario = await StartedAsync();

        await TurnSteps.CompletedAsync(scenario);

        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        Assert.Equal(ArmyTurnStatus.Completed, turn.Status);
        Assert.NotNull(turn.CompletedAt);
        Assert.Equal(
            [ArmyTurnEventKind.Submitted, ArmyTurnEventKind.Approved],
            turn.History.Select(e => e.Kind)
        );
        var email = await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Equal("The Peninsular War: turn 1 approved for First Corps", email.Subject);
    }

    [Fact]
    public async Task ApproveTurn_ADraft_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SendBackTurn_WithNotes_IsADraftAgainWithTheNotesInItsHistory()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(
                "  Too cautious.  ",
                [new UnitNoteDto(scenario.UnitId, "Advance on the ridge.")]
            )
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        Assert.Equal((ArmyTurnStatus.Draft, null), (turn.Status, turn.SubmittedAt));
        var sentBack = turn.History[^1];
        Assert.Equal((ArmyTurnEventKind.SentBack, "Too cautious."), (sentBack.Kind, sentBack.Note));
        Assert.Equal(
            [new UnitNoteDto(scenario.UnitId, "Advance on the ridge.")],
            sentBack.UnitNotes
        );
        var email = await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Contains(
            "1st Division: Advance on the ridge.",
            email.TextBody,
            StringComparison.Ordinal
        );
        Assert.Contains("Their note: Too cautious.", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendBackTurn_KeepsTheOrders()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(null, null)
        );

        response.EnsureSuccessStatusCode();
        var order = Assert.Single((await TurnSteps.OpenArmyTurnAsync(scenario)).Orders);
        Assert.Equal(OrderKind.Hold, order.Kind);
    }

    [Fact]
    public async Task SendBackTurn_NoteOnAUnitInAnotherArmy_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(null, [new UnitNoteDto(Guid.CreateVersion7(), "Where?")])
        );

        await response.AssertValidationProblemAsync("unitNotes");
    }

    [Fact]
    public async Task SendBackTurn_TwoNotesOnOneUnit_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(
                null,
                [new UnitNoteDto(scenario.UnitId, "One."), new UnitNoteDto(scenario.UnitId, "Two.")]
            )
        );

        await response.AssertValidationProblemAsync("unitNotes");
    }

    [Fact]
    public async Task SendBackTurn_AnEmptyUnitNote_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(null, [new UnitNoteDto(scenario.UnitId, "   ")])
        );

        await response.AssertValidationProblemAsync("unitNotes[0].text");
    }

    [Fact]
    public async Task SendBackTurn_NoteTooLong_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        var submitted = await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            submitted.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(new string('x', 2001), null)
        );

        await response.AssertValidationProblemAsync("note");
    }

    [Fact]
    public async Task SendBackTurn_ADraft_Returns409()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            turn.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest(null, null)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevertTurn_Completed_IsADraftAgainAndEmailsTheCommander()
    {
        using var scenario = await StartedAsync();
        var completed = await TurnSteps.CompletedAsync(scenario);

        using var response = await TurnSteps.ActAsync(
            scenario,
            completed.Id,
            "revert",
            Role.Umpire,
            new ReviewTurnRequest("Rethink this.", null)
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        Assert.Equal((ArmyTurnStatus.Draft, null), (turn.Status, turn.CompletedAt));
        Assert.Equal(ArmyTurnEventKind.Reverted, turn.History[^1].Kind);
        await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Contains(
            "The Peninsular War: turn 1 reopened for First Corps",
            Emails.Sent.Select(e => e.Subject),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task RevertTurn_InAClosedTurn_Returns409()
    {
        using var scenario = await StartedAsync();
        var first = await TurnSteps.CompletedAsync(scenario);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);
        next.EnsureSuccessStatusCode();

        using var response = await TurnSteps.ActAsync(
            scenario,
            first.Id,
            "revert",
            Role.Umpire,
            new ReviewTurnRequest(null, null)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RevertTurn_AUnitPlacedSinceTheStart_StaysOnTheMap()
    {
        using var scenario = await StartedAsync();
        var completed = await TurnSteps.CompletedAsync(scenario);
        var added = await AddUnitAsync(scenario, place: true);

        using var response = await TurnSteps.ActAsync(
            scenario,
            completed.Id,
            "revert",
            Role.Umpire,
            new ReviewTurnRequest(null, null)
        );

        response.EnsureSuccessStatusCode();
        var positions = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<UnitPosition>>($"/api/campaigns/{scenario.CampaignId}/positions");
        Assert.Contains(positions!, p => p.UnitId == added);
    }

    [Fact]
    public async Task StartNextTurn_EveryArmyCompleted_OpensTheNextTurnAndEmailsCommanders()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.CompletedAsync(scenario);

        using var response = await TurnSteps.StartNextTurnAsync(scenario);

        var turns = await response.Content.ReadAsAsync<CampaignTurnsResponse>();
        Assert.Equal((2, 3), (turns?.OpenTurn, turns?.Turns.Count));
        Assert.Equal(ArmyTurnStatus.Draft, (await TurnSteps.OpenArmyTurnAsync(scenario)).Status);
        await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Contains(
            "The Peninsular War: turn 2 has started",
            Emails.Sent.Select(e => e.Subject),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task StartNextTurn_AnArmyNotCompleted_Returns409AndSaysWhy()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.SubmittedAsync(scenario);

        using var response = await TurnSteps.StartNextTurnAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
        var turns = await scenario
            .As(Role.Umpire)
            .GetAsAsync<CampaignTurnsResponse>($"/api/campaigns/{scenario.CampaignId}/turns");
        Assert.Equal(["Approve or send back First Corps's turn."], turns!.StartProblems);
    }

    [Fact]
    public async Task StartNextTurn_AUnitNotYetPlaced_Returns409()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.CompletedAsync(scenario);
        await AddUnitAsync(scenario, place: false);

        using var response = await TurnSteps.StartNextTurnAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task StartNextTurn_BeforeTheStart_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.ReadyAsync(scenario);

        using var response = await TurnSteps.StartNextTurnAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GiveOrder_InTheNextTurn_MeasuresFromWhereTheLastTurnLeftTheUnit()
    {
        using var scenario = await StartedAsync();
        var first = await TurnSteps.OpenArmyTurnAsync(scenario);
        // Turn 1 leaves the unit in (0, -2); from there, (1, -2) is the next hex.
        using var moved = await TurnSteps.OrderAsync(
            scenario,
            first.Id,
            TurnSteps.Move(new Hex(0, -1), new Hex(0, -2))
        );
        using var submitted = await TurnSteps.ActAsync(
            scenario,
            first.Id,
            "submit",
            Role.Commander
        );
        using var approved = await TurnSteps.ActAsync(scenario, first.Id, "approve", Role.Umpire);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);
        next.EnsureSuccessStatusCode();
        var second = await TurnSteps.OpenArmyTurnAsync(scenario);

        using var response = await TurnSteps.OrderAsync(
            scenario,
            second.Id,
            TurnSteps.Move(new Hex(1, -2))
        );

        response.EnsureSuccessStatusCode();
    }

    private static (int Q, int R) HexOf(UnitPosition position) => (position.Q, position.R);

    private static async Task<List<UnitPosition>> PositionsAsync(
        CampaignScenario scenario,
        int? turn
    ) =>
        await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<UnitPosition>>(
                $"/api/campaigns/{scenario.CampaignId}/positions{(turn is { } t ? $"?turn={t}" : "")}"
            )
        ?? [];

    [Fact]
    public async Task ListPositions_AClosedTurn_IsWhereUnitsWereAfterIt()
    {
        using var scenario = await StartedAsync();
        var first = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var moved = await TurnSteps.OrderAsync(
            scenario,
            first.Id,
            TurnSteps.Move(new Hex(0, -1), new Hex(0, -2))
        );
        using var submitted = await TurnSteps.ActAsync(
            scenario,
            first.Id,
            "submit",
            Role.Commander
        );
        using var approved = await TurnSteps.ActAsync(scenario, first.Id, "approve", Role.Umpire);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);
        next.EnsureSuccessStatusCode();

        Assert.Equal((0, 0), HexOf(Assert.Single(await PositionsAsync(scenario, 0))));
        Assert.Equal((0, -2), HexOf(Assert.Single(await PositionsAsync(scenario, 1))));
        Assert.Equal((0, -2), HexOf(Assert.Single(await PositionsAsync(scenario, null))));
    }

    [Fact]
    public async Task ListPositions_AClosedTurn_IncludesAUnitPlacedSinceItsArmySubmitted()
    {
        using var scenario = await StartedAsync();
        await TurnSteps.CompletedAsync(scenario);
        var added = await AddUnitAsync(scenario, place: true);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);
        next.EnsureSuccessStatusCode();

        var positions = await PositionsAsync(scenario, 1);

        Assert.Equal(
            [(scenario.UnitId, 1), (added, 0)],
            positions.Select(p => (p.UnitId, p.Turn)).OrderByDescending(p => p.Turn)
        );
    }

    [Fact]
    public async Task ListPositions_TheOpenTurn_IsItsOrdersWhateverTheirStatus()
    {
        using var scenario = await StartedAsync();
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var moved = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            TurnSteps.Move(new Hex(0, -1), new Hex(0, -2))
        );

        var position = Assert.Single(await PositionsAsync(scenario, 1));

        Assert.Equal((ArmyTurnStatus.Draft, (0, -2)), (position.Status, HexOf(position)));
    }
}
