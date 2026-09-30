using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>A campaign's calendar: its turns' days and times of day, and the marches (step 45).</summary>
public sealed class CalendarTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>East from the start, inside the Waterloo grid: flat, half a turn a hex for infantry.</summary>
    private static readonly Hex[] East = [new(1, 0), new(2, -1), new(3, -1)];

    private static async Task<CampaignCalendarResponse> CalendarAsync(
        CampaignScenario scenario,
        Role role = Role.Player
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<CampaignCalendarResponse>(
                    $"/api/campaigns/{scenario.CampaignId}/calendar"
                )
        )!;

    private static async Task<CampaignTurnsResponse> TurnsAsync(CampaignScenario scenario) =>
        (
            await scenario
                .As(Role.Player)
                .GetAsAsync<CampaignTurnsResponse>($"/api/campaigns/{scenario.CampaignId}/turns")
        )!;

    /// <summary>
    /// A unit from a faction of `nation` (None: its army's), in the scenario's army, placed at the
    /// start beside the scenario's own; the campaign then started.
    /// </summary>
    private static async Task<Guid> StartedWithUnitOfAsync(
        CampaignScenario scenario,
        Nation nation,
        string name
    )
    {
        var admin = scenario.As(Role.Admin);
        var faction = await LibrarySteps.CreateFactionAsync(admin, name, nation);
        var unit = await LibrarySteps.CreateUnitAsync(admin, faction, $"{name} Line");
        await LibrarySteps.ChooseFactionsAsync(scenario.As(Role.Umpire), scenario.ArmyId, faction);
        using var added = await LibrarySteps.AddAsync(
            scenario.As(Role.Umpire),
            scenario.ArmyId,
            unit
        );
        var id = (await added.Content.ReadAsAsync<List<ArmyUnitResponse>>())![0].Id;
        await TurnSteps.ReadyAsync(scenario);
        using var placed = await TurnSteps.PlaceAsync(scenario, id);
        placed.EnsureSuccessStatusCode();
        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return id;
    }

    private static async Task<HttpResponseMessage> MoveAsync(
        CampaignScenario scenario,
        Guid unitId,
        params Hex[] path
    )
    {
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        return await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(path), unitId);
    }

    [Fact]
    public async Task GetCampaignCalendar_Untouched_IsTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var calendar = await CalendarAsync(scenario);

        Assert.Null(calendar.StartDate);
        Assert.Equal(TurnPart.Morning, calendar.FirstTurnPart);
        Assert.True(calendar.Rules);
        Assert.Contains(Nation.France, calendar.MorningNations);
        Assert.DoesNotContain(Nation.Saxony, calendar.MorningNations);
        Assert.Equal([Nation.Russia, Nation.Austria], calendar.AfternoonNations);
    }

    [Fact]
    public async Task UpdateCampaignCalendar_LabelsEachTurnWithItsDayAndTimeOfDay()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario, TurnPart.Afternoon, new DateOnly(1815, 6, 15));
        await TurnSteps.StartedAsync(scenario);
        await TurnSteps.CompletedAsync(scenario);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);
        next.EnsureSuccessStatusCode();

        var turns = (await TurnsAsync(scenario)).Turns;

        Assert.Equal(
            [
                (0, (TurnPart?)null, (DateOnly?)null),
                (1, TurnPart.Afternoon, new DateOnly(1815, 6, 15)),
                (2, TurnPart.Night, new DateOnly(1815, 6, 15)),
            ],
            turns.Select(t => (t.Number, t.Part, t.Date))
        );
    }

    [Fact]
    public async Task UpdateCampaignCalendar_AfterANight_IsTheNextDay()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario, TurnPart.Night, new DateOnly(1815, 6, 17));
        await TurnSteps.StartedAsync(scenario);
        await TurnSteps.CompletedAsync(scenario);
        using var next = await TurnSteps.StartNextTurnAsync(scenario);

        var turn = (await TurnsAsync(scenario)).Turns[^1];

        Assert.Equal((TurnPart.Morning, new DateOnly(1815, 6, 18)), (turn.Part, turn.Date));
    }

    [Fact]
    public async Task UpdateCampaignCalendar_TheRulesNations_IsTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        await TurnSteps.SetCalendarAsync(
            scenario,
            morning: [.. (await CalendarAsync(scenario)).MorningNations.Reverse()],
            afternoon: [Nation.Austria, Nation.Russia]
        );

        Assert.True((await CalendarAsync(scenario)).Rules);
    }

    [Fact]
    public async Task UpdateCampaignCalendar_NoNation_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/calendar", UriKind.Relative),
                new UpdateCampaignCalendarRequest(null, TurnPart.Morning, [Nation.None], []),
                Token
            );

        await response.AssertValidationProblemAsync("morningNations");
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateCampaignCalendar_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/calendar", UriKind.Relative),
                new UpdateCampaignCalendarRequest(null, TurnPart.Night, [], []),
                Token
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_FrenchInfantryInTheMorning_GoAFlatHexFurther()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var unit = await StartedWithUnitOfAsync(scenario, Nation.France, "French Guard");

        using var response = await MoveAsync(scenario, unit, East);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_SaxonsInAFrenchArmy_GoNoFurtherInTheMorning()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // The unit's faction decides, not its army (which the scenario gives no nation anyway).
        var unit = await StartedWithUnitOfAsync(scenario, Nation.Saxony, "Saxon");

        using var three = await MoveAsync(scenario, unit, East);
        using var two = await MoveAsync(scenario, unit, East[..2]);

        await three.AssertValidationProblemAsync("path");
        Assert.Equal(HttpStatusCode.OK, two.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_RussiansInTheAfternoon_GoAFlatHexLess()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario, TurnPart.Afternoon, afternoon: [Nation.Russia]);
        var unit = await StartedWithUnitOfAsync(scenario, Nation.Russia, "Russian");

        using var two = await MoveAsync(scenario, unit, East[..2]);
        using var one = await MoveAsync(scenario, unit, East[..1]);

        await two.AssertValidationProblemAsync("path");
        Assert.Equal(HttpStatusCode.OK, one.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_AFactionWithoutANation_MarchesAsItsArmy()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var armySetUp = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new Wwg.Api.Features.Armies.UpdateArmyRequest(
                    "First Corps",
                    scenario.SideId,
                    ArmyColor.Blue,
                    Nation.France
                ),
                Token
            );
        var unit = await StartedWithUnitOfAsync(scenario, Nation.None, "Mixed");

        var units = await scenario
            .As(Role.Player)
            .GetAsAsync<List<ArmyUnitResponse>>($"/api/campaigns/{scenario.CampaignId}/units");
        using var response = await MoveAsync(scenario, unit, East);

        Assert.Equal(Nation.France, units!.Single(u => u.Id == unit).Nation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GiveOrder_FrenchCavalryInTheMorning_GoNoFurther()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var faction = await LibrarySteps.CreateFactionAsync(
            scenario.As(Role.Admin),
            "Chasseurs",
            Nation.France
        );
        var library = await LibrarySteps.CreateUnitAsync(
            scenario.As(Role.Admin),
            faction,
            "Chasseurs",
            UnitType.LightCavalry
        );
        await LibrarySteps.ChooseFactionsAsync(scenario.As(Role.Umpire), scenario.ArmyId, faction);
        using var added = await LibrarySteps.AddAsync(
            scenario.As(Role.Umpire),
            scenario.ArmyId,
            library
        );
        var unit = (await added.Content.ReadAsAsync<List<ArmyUnitResponse>>())![0].Id;
        await TurnSteps.ReadyAsync(scenario);
        using var placed = await TurnSteps.PlaceAsync(scenario, unit);
        using var started = await TurnSteps.StartAsync(scenario);

        // Light cavalry: four flat hexes, Morning or not (the rules' bonus is infantry's).
        using var five = await MoveAsync(scenario, unit, [.. East, new(3, -2), new(2, -2)]);

        await five.AssertValidationProblemAsync("path");
    }
}
