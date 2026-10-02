using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Attrition from forced marches, and each unit's points history (step 47, decision 0018).</summary>
public sealed class AttritionTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>
    /// Started, turn 1 a Morning and no nation marching further or less, with the scenario's unit
    /// given this Fighting Factor and these points.
    /// </summary>
    private async Task<CampaignScenario> StartedAsync(int fightingFactor = 2, int points = 50)
    {
        var scenario = await CreateCampaignScenarioAsync();
        using var set = await EditAsync(scenario, fightingFactor, points);
        set.EnsureSuccessStatusCode();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.StartedAsync(scenario);
        return scenario;
    }

    private static Task<HttpResponseMessage> EditAsync(
        CampaignScenario scenario,
        int fightingFactor,
        int points
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/army-units/{scenario.UnitId}", UriKind.Relative),
                new UpdateArmyUnitRequest(
                    "1st Division",
                    UnitType.LineInfantry,
                    fightingFactor,
                    points
                ),
                Token
            );

    /// <summary>Plays the turns, then gives the open one a move, submitted and approved.</summary>
    private static async Task MovedAsync(CampaignScenario scenario, int turnsBefore)
    {
        await TurnSteps.PlayAsync(scenario, [.. Enumerable.Repeat("move", turnsBefore)]);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        var there =
            await TurnSteps.HereAsync(scenario) == TurnSteps.Start
                ? new Hex(1, 0)
                : TurnSteps.Start;
        using var given = await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(there));
        given.EnsureSuccessStatusCode();
        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Commander);
        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);
    }

    private static Task<HttpResponseMessage> StartNextAsync(
        CampaignScenario scenario,
        params AttritionLossRequest[] losses
    ) => TurnSteps.StartNextTurnAsync(scenario, request: new(losses));

    private static async Task<ArmyUnitResponse> UnitAsync(CampaignScenario scenario) =>
        (
            await scenario
                .As(Role.Player)
                .GetAsAsync<List<ArmyUnitResponse>>($"/api/campaigns/{scenario.CampaignId}/units")
        )!.Single(u => u.Id == scenario.UnitId);

    private static async Task<List<PointsChangeResponse>> HistoryAsync(
        CampaignScenario scenario,
        Role role = Role.Player
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<PointsChangeResponse>>($"/api/army-units/{scenario.UnitId}/points")
        )!;

    [Fact]
    public async Task ListAttritionDue_TheFirstForcedMarch_IsFree()
    {
        using var scenario = await StartedAsync();

        await MovedAsync(scenario, turnsBefore: 2);

        Assert.Empty(await TurnSteps.AttritionDueAsync(scenario));
    }

    [Fact]
    public async Task ListAttritionDue_TheSecondTurnOfForcedMarch_IsTheScale()
    {
        using var scenario = await StartedAsync();

        await MovedAsync(scenario, turnsBefore: 3);
        var due = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));

        // FF 2, 50 points: the scale's 3 points, normal attrition.
        Assert.Equal(
            (scenario.UnitId, 2, 1, 3),
            (due.UnitId, due.ForcedMarchTurns, due.Multiplier, due.Loss)
        );
    }

    [Fact]
    public async Task ListAttritionDue_ALargerUnitAndALaterTurn_LoseMore()
    {
        using var scenario = await StartedAsync(fightingFactor: 4, points: 100);

        await MovedAsync(scenario, turnsBefore: 4);

        // FF 4 is 2 points for 50: turn 4 cost 4 of its 100; turn 5, doubled, 7.68 of 96.
        var due = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));
        Assert.Equal((96, 2, 7), (due.Points, due.Multiplier, due.Loss));
    }

    [Fact]
    public async Task StartNextTurn_WithoutTheAttrition_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        await MovedAsync(scenario, turnsBefore: 3);

        using var response = await TurnSteps.StartNextTurnAsync(scenario);

        await response.AssertValidationProblemAsync("attrition");
        Assert.Equal(50, (await UnitAsync(scenario)).Points);
    }

    [Fact]
    public async Task StartNextTurn_TheConfirmedLoss_IsTakenAndRecorded()
    {
        using var scenario = await StartedAsync();
        await MovedAsync(scenario, turnsBefore: 3);

        // The Umpire makes it 2, not 3.
        using var response = await StartNextAsync(
            scenario,
            new AttritionLossRequest(scenario.UnitId, 2)
        );
        response.EnsureSuccessStatusCode();
        var change = Assert.Single(await HistoryAsync(scenario));

        Assert.Equal(48, (await UnitAsync(scenario)).Points);
        Assert.Equal(
            (4, -2, 48, PointsChangeReason.Attrition, "Forced march"),
            (change.Turn, change.Change, change.PointsAfter, change.Reason, change.Note)
        );
    }

    [Fact]
    public async Task StartNextTurn_TheConfirmedLoss_IsInTheCommandersTurnEmail()
    {
        using var scenario = await StartedAsync();
        await MovedAsync(scenario, turnsBefore: 3);

        using var response = await StartNextAsync(
            scenario,
            new AttritionLossRequest(scenario.UnitId, 2)
        );
        response.EnsureSuccessStatusCode();

        var email = await Emails.WaitForEmailToAsync("commander@example.com", "turn 5 has started");
        Assert.Contains(
            "- 1st Division lost 2 points to attrition (forced march); now 48 points.",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StartNextTurn_MoreThanTheUnitHas_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        await MovedAsync(scenario, turnsBefore: 3);

        using var response = await StartNextAsync(
            scenario,
            new AttritionLossRequest(scenario.UnitId, 51)
        );

        await response.AssertValidationProblemAsync("attrition");
    }

    [Fact]
    public async Task StartNextTurn_AUnitThatOwesNothing_IsAValidationError()
    {
        using var scenario = await StartedAsync();
        await MovedAsync(scenario, turnsBefore: 0);

        using var response = await StartNextAsync(
            scenario,
            new AttritionLossRequest(scenario.UnitId, 1)
        );

        await response.AssertValidationProblemAsync("attrition");
    }

    [Fact]
    public async Task StartNextTurn_HalfAPointATurn_LosesOneEveryOtherTurn()
    {
        // FF 7, 50 points: half a point a turn of normal attrition.
        using var scenario = await StartedAsync(fightingFactor: 7);
        await TurnSteps.PlayAsync(scenario, "move", "move", "move");
        // Turns 4 and 5 force march on: ×1, then ×2.
        await MovedAsync(scenario, turnsBefore: 0);
        var first = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));
        using var started = await StartNextAsync(
            scenario,
            new AttritionLossRequest(scenario.UnitId, first.Loss)
        );
        await MovedAsync(scenario, turnsBefore: 0);
        var second = Assert.Single(await TurnSteps.AttritionDueAsync(scenario));

        // ½ (carried), then ½ + 1: one point, ½ carried on.
        Assert.Equal((0, 1), (first.Loss, second.Loss));
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListAttritionDue_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/attrition", UriKind.Relative),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UpdateArmyUnit_PointsOnceStarted_GoInTheHistory()
    {
        using var scenario = await StartedAsync();

        using var edited = await EditAsync(scenario, 2, 45);
        var change = Assert.Single(await HistoryAsync(scenario));

        Assert.Equal(
            (1, -5, 45, PointsChangeReason.Edited),
            (change.Turn, change.Change, change.PointsAfter, change.Reason)
        );
        Assert.NotNull(change.ByName);
    }

    [Fact]
    public async Task UpdateArmyUnit_BeforeTheStart_LeavesNoHistory()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var edited = await EditAsync(scenario, 2, 45);

        Assert.Empty(await HistoryAsync(scenario));
    }

    [Theory]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListPointsHistory_ByRole_EveryMember(Role role, HttpStatusCode expected)
    {
        using var scenario = await StartedAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/army-units/{scenario.UnitId}/points", UriKind.Relative),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
