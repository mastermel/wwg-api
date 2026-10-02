using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Sightings;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// The sightings the Umpire shapes as each turn starts (step 49b, decision 0020). The scenario's
/// unit is at (0, 0); an army of the other side, with a brigade at (1, 0), from the start.
/// </summary>
public sealed class SightingTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static readonly Hex Brigade = new(1, 0);

    private async Task<(CampaignScenario Scenario, Guid Enemy)> StartedAsync()
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario);
        await TurnSteps.ReadyAsync(scenario);
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Prussians", null, scenario.OtherSideId),
                Token
            );
        var enemy = (await created.Content.ReadAsAsync<ArmyResponse>())!.Id;
        var unit = await LibrarySteps.AddUnitAsync(scenario, "Brigade", points: 30, armyId: enemy);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit, Brigade);
        placed.EnsureSuccessStatusCode();
        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return (scenario, enemy);
    }

    /// <summary>Both armies hold and are approved; the next turn starts with these sightings.</summary>
    private static async Task<HttpResponseMessage> NextTurnAsync(
        CampaignScenario scenario,
        Guid enemy,
        params SightingRequest[] sightings
    )
    {
        await TurnSteps.CompletedAsync(scenario);
        var umpire = scenario.As(Role.Umpire);
        var turns = await umpire.GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{enemy}/turns");
        var turn = turns!.Single(t => t.Open);
        var units = await umpire.GetAsAsync<List<Wwg.Api.Features.ArmyUnits.ArmyUnitResponse>>(
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
        return await TurnSteps.StartNextTurnAsync(scenario, request: new([], sightings));
    }

    private static SightingRequest Everything(CampaignScenario scenario, Hex? at = null) =>
        new(
            scenario.ArmyId,
            (at ?? Brigade).Q,
            (at ?? Brigade).R,
            true,
            true,
            true,
            SightingStrength.Rough,
            ForceSize.Medium
        );

    private static async Task<List<SightingResponse>> SightingsAsync(
        CampaignScenario scenario,
        Role role = Role.Commander
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<SightingResponse>>(
                    $"/api/campaigns/{scenario.CampaignId}/sightings"
                )
        )!;

    [Fact]
    public async Task StartNextTurn_AConfirmedSighting_IsTheArmysForTheTurn()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(scenario, enemy, Everything(scenario));
        next.EnsureSuccessStatusCode();
        var seen = Assert.Single(await SightingsAsync(scenario));

        Assert.Equal((2, 1, 0), (seen.Turn, seen.Q, seen.R));
        Assert.Equal([enemy], seen.ArmyIds);
        Assert.Equal([UnitType.LineInfantry], seen.UnitTypes);
        Assert.Equal(
            (SightingStrength.Rough, ForceSize.Medium, (int?)null),
            (seen.Strength, seen.Size, seen.Points)
        );
        Assert.False(seen.ByUmpire);
    }

    [Fact]
    public async Task StartNextTurn_AConfirmedSighting_IsInTheCommandersTurnEmail()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(scenario, enemy, Everything(scenario));
        next.EnsureSuccessStatusCode();

        var email = await Emails.WaitForEmailToAsync("commander@example.com", "turn 2 has started");
        Assert.Contains("Enemy sightings", email.TextBody, StringComparison.Ordinal);
        Assert.Contains(
            "- Hex (1, 0): Prussians: 1 line infantry, a medium force.",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StartNextTurn_TheHexKeptBack_GivesOnlyRoughlyWhere()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(
            scenario,
            enemy,
            Everything(scenario) with
            {
                ShowsHex = false,
                ShowsArmies = false,
                ShowsTypes = false,
                Strength = SightingStrength.Exact,
            }
        );
        var seen = Assert.Single(await SightingsAsync(scenario));

        Assert.Equal(((int?)null, (double?)null), (seen.Q, seen.Latitude));
        Assert.Equal("1 hex south-east of 1st Division", seen.Whereabouts);
        Assert.Null(seen.ArmyIds);
        Assert.Null(seen.UnitTypes);
        Assert.Equal(30, seen.Points);
    }

    [Fact]
    public async Task StartNextTurn_ASightingLeftOut_IsntSeen()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(scenario, enemy);
        next.EnsureSuccessStatusCode();

        Assert.Empty(await SightingsAsync(scenario));
    }

    [Fact]
    public async Task StartNextTurn_ASightingTheAppDidntFind_IsTheUmpiresOwn()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;
        // A second brigade, far out of sight: a spy reports it.
        var far = await LibrarySteps.AddUnitAsync(scenario, "Far brigade", armyId: enemy);
        using var placed = await TurnSteps.PlaceAsync(scenario, far, new Hex(3, -1));

        using var next = await NextTurnAsync(
            scenario,
            enemy,
            Everything(scenario),
            Everything(scenario, new Hex(3, -1))
        );
        next.EnsureSuccessStatusCode();
        var seen = await SightingsAsync(scenario);

        Assert.Equal([false, true], seen.OrderBy(s => s.Q).Select(s => s.ByUmpire));
    }

    [Fact]
    public async Task StartNextTurn_AHexWithNoEnemyThere_IsAValidationError()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(scenario, enemy, Everything(scenario, new Hex(2, -1)));

        await next.AssertValidationProblemAsync("sightings");
    }

    [Fact]
    public async Task StartNextTurn_ARoughStrengthWithoutASize_IsAValidationError()
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;

        using var next = await NextTurnAsync(
            scenario,
            enemy,
            Everything(scenario) with
            {
                Size = null,
            }
        );

        await next.AssertValidationProblemAsync("sightings");
    }

    [Theory]
    [InlineData(Role.Umpire, 1)]
    [InlineData(Role.Commander, 1)]
    [InlineData(Role.Player, 0)]
    public async Task ListSightings_ByRole_TheObservingArmysCommanderAndTheUmpire(
        Role role,
        int seen
    )
    {
        var (scenario, enemy) = await StartedAsync();
        using var _ = scenario;
        using var next = await NextTurnAsync(scenario, enemy, Everything(scenario));

        Assert.Equal(seen, (await SightingsAsync(scenario, role)).Count);
    }

    [Fact]
    public async Task ListSightings_ByANonMember_Returns404()
    {
        var (scenario, _) = await StartedAsync();
        using var __ = scenario;

        using var response = await scenario
            .As(Role.NonMember)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/sightings", UriKind.Relative),
                Token
            );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
