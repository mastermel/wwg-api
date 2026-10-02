using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Intelligence;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Sightings;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Reports between allies, by courier (step 49c, decision 0020). The scenario's unit is at (0, 0);
/// an ally army of the same side, commanded by the scenario's Player, has a unit where each test
/// says, from the start.
/// </summary>
public sealed class IntelTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>Started, with the ally (and, if given, an enemy) placed; hexes of the size given.</summary>
    private async Task<(CampaignScenario Scenario, Guid Ally)> StartedAsync(
        Hex allyAt,
        int hexSize = CampaignMap.DefaultHexSize,
        Hex? enemyAt = null
    )
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.SetAreaAsync(scenario, hexSize);
        using var placed = await TurnSteps.PlaceAsync(scenario, scenario.UnitId);
        var ally = await ArmyAsync(
            scenario,
            "Allies",
            scenario.SideId,
            scenario.PlayerMemberId,
            allyAt
        );
        if (enemyAt is { } at)
        {
            await ArmyAsync(scenario, "Enemy", scenario.OtherSideId, null, at);
        }
        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return (scenario, ally);
    }

    private static async Task<Guid> ArmyAsync(
        CampaignScenario scenario,
        string name,
        Guid side,
        Guid? commander,
        Hex at
    )
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest(name, commander, side),
                Token
            );
        var army = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var unit = await LibrarySteps.AddUnitAsync(scenario, $"{name} brigade", armyId: army);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, at);
        placed.EnsureSuccessStatusCode();
        return army;
    }

    private static Task<HttpResponseMessage> SendAsync(
        CampaignScenario scenario,
        SendReportRequest request,
        Role role = Role.Commander
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/reports", UriKind.Relative),
                request,
                Token
            );

    private static SendReportRequest Everything(Guid ally) =>
        new(ally, true, true, "Hold the bridge at Wavre.");

    /// <summary>Every army holds and is approved (the Umpire submitting for those with none), and the next turn starts.</summary>
    private static async Task NextTurnAsync(CampaignScenario scenario)
    {
        var umpire = scenario.As(Role.Umpire);
        var armies = await umpire.GetAsAsync<List<ArmySummary>>(
            $"/api/campaigns/{scenario.CampaignId}/armies"
        );
        var units = await umpire.GetAsAsync<List<ArmyUnitResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/units"
        );
        foreach (var army in armies!)
        {
            var turn = (
                await umpire.GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{army.Id}/turns")
            )!.Single(t => t.Open);
            foreach (var unit in units!.Where(u => u.ArmyId == army.Id))
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
            using var submitted = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "submit",
                Role.Umpire
            );
            using var approved = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "approve",
                Role.Umpire
            );
            approved.EnsureSuccessStatusCode();
        }
        using var next = await TurnSteps.StartNextTurnAsync(scenario, request: new([], []));
        next.EnsureSuccessStatusCode();
    }

    private static async Task<List<ReportResponse>> ReportsAsync(
        CampaignScenario scenario,
        Role role
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<ReportResponse>>($"/api/campaigns/{scenario.CampaignId}/reports")
        )!;

    private static async Task<List<CourierResponse>> CouriersAsync(CampaignScenario scenario) =>
        (
            await scenario
                .As(Role.Umpire)
                .GetAsAsync<List<CourierResponse>>($"/api/campaigns/{scenario.CampaignId}/couriers")
        )!;

    [Fact]
    public async Task SendReport_ToAnAllyCloseBy_ArrivesAsTheNextTurnStarts()
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;

        using var sent = await SendAsync(scenario, Everything(ally));
        sent.EnsureSuccessStatusCode();
        var before = await ReportsAsync(scenario, Role.Player);
        await NextTurnAsync(scenario);
        var received = Assert.Single(await ReportsAsync(scenario, Role.Player));

        Assert.Empty(before);
        Assert.Equal(
            (1, 2, CourierStatus.Arrived),
            (received.SentTurn, received.ArrivedTurn, received.Status)
        );
        Assert.Equal("Hold the bridge at Wavre.", received.Note);
        var unit = Assert.Single(received.Snapshot!);
        Assert.Equal(("1st Division", 0, 0, 20), (unit.Name, unit.Q, unit.R, unit.Points));
    }

    [Fact]
    public async Task StartNextTurn_AReportArriving_IsInTheAllysTurnEmail_WithItsMessage()
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;
        using var sent = await SendAsync(scenario, Everything(ally));
        sent.EnsureSuccessStatusCode();

        await NextTurnAsync(scenario);

        var email = await Emails.WaitForEmailToAsync("player@example.com", "turn 2 has started");
        Assert.Contains("Reports from allies", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(
            "From First Corps (their units' positions)",
            email.TextBody,
            StringComparison.Ordinal
        );
        Assert.Contains("> Hold the bridge at Wavre.", email.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendReport_ARiverBridgedFarOff_TheCourierRidesRoundByTheBridge()
    {
        // 1 km hexes: the river runs the area's whole height, between column 0 and column 1,
        // bridged only 10 hexes north, well beyond the courier and its ally.
        var (scenario, ally) = await StartedAsync(new Hex(6, 0), hexSize: 1000);
        using var _ = scenario;
        await RiverAsync(scenario, bridgedAt: -10);
        using var sent = await SendAsync(scenario, Everything(ally));
        sent.EnsureSuccessStatusCode();

        for (var turn = 0; turn < 8; turn++)
        {
            await NextTurnAsync(scenario);
        }

        Assert.Single(await ReportsAsync(scenario, Role.Player));
    }

    /// <summary>A river between column 0 and column 1, as far as the grid runs, bridged once.</summary>
    private static async Task RiverAsync(CampaignScenario scenario, int bridgedAt)
    {
        for (var r = -20; r <= 20; r++)
        {
            var river = new UpdateHexEdgeRequest(
                RoadQuality.None,
                true,
                r == bridgedAt,
                Waterway.None
            );
            foreach (var side in new[] { "NE", "SE" })
            {
                // Outside the grid is refused: the river runs as far as the grid does.
                using var edge = await scenario
                    .As(Role.Umpire)
                    .PutAsJsonAsync(
                        new Uri(
                            $"/api/campaigns/{scenario.CampaignId}/grid/edges/0/{r}/{side}",
                            UriKind.Relative
                        ),
                        river,
                        Token
                    );
            }
        }
    }

    [Fact]
    public async Task SendReport_ToAnAllyFarOff_RidesATurnAtATime()
    {
        // 1 km hexes: the ally 9 hexes away, more than two turns' ride for light cavalry (4 a turn).
        var (scenario, ally) = await StartedAsync(new Hex(9, -4), hexSize: 1000);
        using var _ = scenario;

        using var sent = await SendAsync(scenario, Everything(ally));
        var setOut = Assert.Single(await CouriersAsync(scenario));
        await NextTurnAsync(scenario);
        var riding = Assert.Single(await CouriersAsync(scenario));

        Assert.Equal((0, 0, false), (setOut.Q, setOut.R, setOut.ArrivesNext));
        Assert.Equal(5, new Hex(riding.Q, riding.R).Distance(new Hex(9, -4)));
        Assert.Empty(await ReportsAsync(scenario, Role.Player));
    }

    [Fact]
    public async Task SendReport_TheSendersSightings_BecomeTheAllysOnArrival()
    {
        var (scenario, ally) = await StartedAsync(new Hex(-1, 0), enemyAt: new Hex(1, 0));
        using var _ = scenario;
        var umpire = scenario.As(Role.Umpire);
        // Turn 2 starts with the scenario's army seeing the enemy next to it.
        var armies = await umpire.GetAsAsync<List<ArmySummary>>(
            $"/api/campaigns/{scenario.CampaignId}/armies"
        );
        await NextTurnWithSightingAsync(scenario, armies!);
        using var sent = await SendAsync(scenario, new SendReportRequest(ally, false, true, null));
        await NextTurnAsync(scenario);

        var theirs = await umpire.GetAsAsync<List<SightingResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/sightings"
        );
        var shared = Assert.Single(theirs!, s => s.ObservingArmyId == ally);

        Assert.Equal((2, scenario.ArmyId), (shared.Turn, shared.SharedByArmyId));
    }

    [Fact]
    public async Task SendReport_SightingsSentBackByTheAlly_ArentHandedBackToTheirFirstObserver()
    {
        var (scenario, ally) = await StartedAsync(new Hex(-1, 0), enemyAt: new Hex(1, 0));
        using var _ = scenario;
        var umpire = scenario.As(Role.Umpire);
        var armies = await umpire.GetAsAsync<List<ArmySummary>>(
            $"/api/campaigns/{scenario.CampaignId}/armies"
        );
        await NextTurnWithSightingAsync(scenario, armies!);
        using var sent = await SendAsync(scenario, new SendReportRequest(ally, false, true, null));
        sent.EnsureSuccessStatusCode();
        await NextTurnAsync(scenario);

        // The ally sends everything it has back, the shared sighting among it.
        using var back = await scenario
            .As(Role.Player)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{ally}/reports", UriKind.Relative),
                new SendReportRequest(scenario.ArmyId, false, true, null),
                Token
            );
        back.EnsureSuccessStatusCode();
        await NextTurnAsync(scenario);

        var sightings = await umpire.GetAsAsync<List<SightingResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/sightings"
        );
        Assert.Single(sightings!, s => s.ObservingArmyId == scenario.ArmyId);
    }

    private static async Task NextTurnWithSightingAsync(
        CampaignScenario scenario,
        List<ArmySummary> armies
    )
    {
        var umpire = scenario.As(Role.Umpire);
        var units = await umpire.GetAsAsync<List<ArmyUnitResponse>>(
            $"/api/campaigns/{scenario.CampaignId}/units"
        );
        foreach (var army in armies)
        {
            var turn = (
                await umpire.GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{army.Id}/turns")
            )!.Single(t => t.Open);
            foreach (var unit in units!.Where(u => u.ArmyId == army.Id))
            {
                using var held = await TurnSteps.OrderAsync(
                    scenario,
                    turn.Id,
                    TurnSteps.Hold,
                    unit.Id,
                    Role.Umpire
                );
            }
            using var submitted = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "submit",
                Role.Umpire
            );
            using var approved = await TurnSteps.ActAsync(
                scenario,
                turn.Id,
                "approve",
                Role.Umpire
            );
        }
        using var next = await TurnSteps.StartNextTurnAsync(
            scenario,
            request: new(
                [],
                [
                    new SightingRequest(
                        scenario.ArmyId,
                        1,
                        0,
                        true,
                        true,
                        true,
                        SightingStrength.Hidden
                    ),
                ]
            )
        );
        next.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task SendReport_ToTheOtherSide_IsAValidationError()
    {
        var (scenario, _) = await StartedAsync(new Hex(1, 0), enemyAt: new Hex(-1, 0));
        using var __ = scenario;
        var armies = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<ArmySummary>>($"/api/campaigns/{scenario.CampaignId}/armies");
        var enemy = armies!
            .Single(a => string.Equals(a.Name, "Enemy", StringComparison.Ordinal))
            .Id;

        using var sent = await SendAsync(scenario, Everything(enemy));

        await sent.AssertValidationProblemAsync("toArmyId");
    }

    [Fact]
    public async Task SendReport_WithNothingInIt_IsAValidationError()
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;

        using var sent = await SendAsync(scenario, new SendReportRequest(ally, false, false, " "));

        await sent.AssertValidationProblemAsync("note");
    }

    [Fact]
    public async Task StopCourier_ItNeverArrives()
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;
        using var sent = await SendAsync(scenario, Everything(ally));
        var report = (await sent.Content.ReadAsAsync<ReportResponse>())!.Id;

        using var stopped = await scenario
            .As(Role.Umpire)
            .PostAsync(new Uri($"/api/reports/{report}/stop", UriKind.Relative), null, Token);
        await NextTurnAsync(scenario);

        Assert.Equal(HttpStatusCode.NoContent, stopped.StatusCode);
        Assert.Empty(await ReportsAsync(scenario, Role.Player));
        Assert.Equal(
            CourierStatus.Stopped,
            Assert.Single(await ReportsAsync(scenario, Role.Umpire)).Status
        );
    }

    [Fact]
    public async Task ListReports_TheSender_IsntToldWhetherItArrived()
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;
        using var sent = await SendAsync(scenario, Everything(ally));
        await NextTurnAsync(scenario);

        var mine = Assert.Single(await ReportsAsync(scenario, Role.Commander));

        Assert.Equal(((CourierStatus?)null, (int?)null), (mine.Status, mine.ArrivedTurn));
    }

    [Fact]
    public async Task ListCouriers_AmongTheEnemy_IsFlagged()
    {
        // The enemy in the sender's own hex, where the courier sets out.
        var (scenario, ally) = await StartedAsync(new Hex(1, 0), enemyAt: new Hex(0, 0));
        using var _ = scenario;
        using var sent = await SendAsync(scenario, Everything(ally));

        Assert.True(Assert.Single(await CouriersAsync(scenario)).AmongTheEnemy);
    }

    [Theory]
    [InlineData(Role.Umpire, HttpStatusCode.Created)]
    [InlineData(Role.Commander, HttpStatusCode.Created)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task SendReport_ByRole_TheArmysCommanderOrTheUmpire(
        Role role,
        HttpStatusCode expected
    )
    {
        var (scenario, ally) = await StartedAsync(new Hex(1, 0));
        using var _ = scenario;

        using var sent = await SendAsync(scenario, Everything(ally), role);

        Assert.Equal(expected, sent.StatusCode);
    }

    [Theory]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListCouriers_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        var (scenario, _) = await StartedAsync(new Hex(1, 0));
        using var __ = scenario;

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/couriers", UriKind.Relative),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }
}
