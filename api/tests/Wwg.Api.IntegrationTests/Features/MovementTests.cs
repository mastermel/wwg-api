using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Movement by the terrain and the campaign's movement table (decision 0014, step 44).</summary>
public sealed class MovementTests : ApiTest
{
    // By name: the cases' own type is internal, which a public theory can't take.
    public static TheoryData<string> Steps => [.. MovementFigures.Cases.Select(c => c.Name)];

    private static readonly Hex North = new(0, -1);

    /// <summary>Four steps east from the start, inside the Waterloo grid (it's 4½ hexes tall).</summary>
    private static readonly Hex[] East = [new(1, 0), new(2, -1), new(3, -1), new(3, -2)];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    /// <summary>A started campaign with a unit of `type` at the start hex; its ID.</summary>
    private async Task<(CampaignScenario Scenario, Guid UnitId)> StartedWithAsync(UnitType type)
    {
        var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.ReadyAsync(scenario);
        var unitId =
            type == UnitType.LineInfantry
                ? scenario.UnitId
                : await LibrarySteps.AddUnitAsync(scenario, "Mover", type);
        if (unitId != scenario.UnitId)
        {
            using var placed = await TurnSteps.PlaceAsync(scenario, unitId);
            placed.EnsureSuccessStatusCode();
        }

        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();
        return (scenario, unitId);
    }

    private static async Task SetCellAsync(
        CampaignScenario scenario,
        Hex hex,
        Terrain terrain,
        bool forest
    )
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/cells/{hex.Q}/{hex.R}",
                    UriKind.Relative
                ),
                new UpdateHexCellRequest(terrain, forest, HexSettlement.None),
                Token
            );
        response.EnsureSuccessStatusCode();
    }

    private static async Task SetEdgeAsync(
        CampaignScenario scenario,
        Hex hex,
        EdgeSide side,
        RoadQuality road,
        bool river = false,
        bool bridge = false
    )
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/campaigns/{scenario.CampaignId}/grid/edges/{hex.Q}/{hex.R}/{side}",
                    UriKind.Relative
                ),
                new UpdateHexEdgeRequest(road, river, bridge, Waterway.None),
                Token
            );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> MoveAsync(
        CampaignScenario scenario,
        Guid unitId,
        Role role,
        params Hex[] path
    )
    {
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        return await TurnSteps.OrderAsync(scenario, turn.Id, TurnSteps.Move(path), unitId, role);
    }

    [Theory]
    [MemberData(nameof(Steps))]
    public async Task GiveOrder_OneStep_GoesByTheTable(string name)
    {
        var step = MovementFigures.Cases.Single(c =>
            string.Equals(c.Name, name, StringComparison.Ordinal)
        );
        var (scenario, unitId) = await StartedWithAsync(step.Type);
        using var _ = scenario;
        if (step.Cell is { } cell)
        {
            await SetCellAsync(scenario, North, cell.Terrain, cell.Forest);
        }

        if (step.Edge is { } edge)
        {
            await SetEdgeAsync(
                scenario,
                TurnSteps.Start,
                EdgeSide.N,
                edge.Road,
                edge.River,
                edge.Bridge
            );
        }

        using var response = await MoveAsync(scenario, unitId, Role.Commander, North);

        if (step.Cost is <= Movement.Budget)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        else
        {
            var problem = await response.AssertValidationProblemAsync("path");
            var message = Assert.Single(problem.Errors["path"]);
            Assert.Equal(
                step.ClosedBecause is { } closed
                    ? $"The unit can't go that way: {closed}."
                    : "That's further than the unit can move in a turn.",
                message
            );
        }
    }

    [Fact]
    public async Task GiveOrder_AlongAGoodRoad_GoesThreeHexesForInfantry()
    {
        var (scenario, unitId) = await StartedWithAsync(UnitType.LineInfantry);
        using var _ = scenario;
        var from = TurnSteps.Start;
        foreach (var hex in East)
        {
            // Each edge where the API keeps it: on one of the two hexes' N, NE or SE side.
            var (stored, side) = PathTerrain.Stored(from, hex);
            await SetEdgeAsync(scenario, stored, side, RoadQuality.Good);
            from = hex;
        }

        using var three = await MoveAsync(scenario, unitId, Role.Commander, East[..3]);
        using var four = await MoveAsync(scenario, unitId, Role.Commander, East);

        Assert.Equal(HttpStatusCode.OK, three.StatusCode);
        await four.AssertValidationProblemAsync("path");
    }

    [Fact]
    public async Task GiveOrder_ByTheUmpire_MayCrossAClosedStep()
    {
        var (scenario, unitId) = await StartedWithAsync(UnitType.LineInfantry);
        using var _ = scenario;
        await SetCellAsync(scenario, North, Terrain.Water, false);

        using var response = await MoveAsync(scenario, unitId, Role.Umpire, North);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMovementTable_Untouched_IsTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var table = await scenario
            .As(Role.Player)
            .GetAsAsync<MovementTableResponse>($"/api/campaigns/{scenario.CampaignId}/movement");

        Assert.True(table!.Rules);
        Assert.Equal(30, table.Rates.Count);
        Assert.Contains(
            new MovementRateDto(MovementClass.Infantry, Ground.HighHill, 0.5),
            table.Rates
        );
        Assert.Contains(new MovementRateDto(MovementClass.Slow, Ground.HighHill, 0), table.Rates);
    }

    private static List<MovementRateDto> RulesWith(
        MovementClass movementClass,
        Ground ground,
        double hexes
    ) =>
        [
            .. MovementTable.Rules.Select(rule => new MovementRateDto(
                rule.Key.Item1,
                rule.Key.Item2,
                rule.Key == (movementClass, ground) ? hexes : rule.Value
            )),
        ];

    private static Task<HttpResponseMessage> SaveTableAsync(
        CampaignScenario scenario,
        IReadOnlyList<MovementRateDto> rates,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/movement", UriKind.Relative),
                new SaveMovementTableRequest(rates),
                Token
            );

    [Fact]
    public async Task SaveMovementTable_FasterInfantry_LetsThemGoFurther()
    {
        var (scenario, unitId) = await StartedWithAsync(UnitType.LineInfantry);
        using var _ = scenario;
        var three = East[..3];
        using var before = await MoveAsync(scenario, unitId, Role.Commander, three);

        using var saved = await SaveTableAsync(
            scenario,
            RulesWith(MovementClass.Infantry, Ground.Flat, 3)
        );
        using var after = await MoveAsync(scenario, unitId, Role.Commander, three);

        await before.AssertValidationProblemAsync("path");
        var table = await saved.Content.ReadAsAsync<MovementTableResponse>();
        Assert.False(table!.Rules);
        Assert.Contains(new MovementRateDto(MovementClass.Infantry, Ground.Flat, 3), table.Rates);
        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
    }

    [Fact]
    public async Task SaveMovementTable_AsTheRules_IsTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var saved = await SaveTableAsync(
            scenario,
            RulesWith(MovementClass.Infantry, Ground.Flat, 2)
        );

        Assert.True((await saved.Content.ReadAsAsync<MovementTableResponse>())!.Rules);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("twice")]
    [InlineData("a third")]
    [InlineData("too many")]
    public async Task SaveMovementTable_Wrong_IsAValidationError(string wrong)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var rates = RulesWith(MovementClass.Infantry, Ground.Flat, 2);
        IReadOnlyList<MovementRateDto> sent = wrong switch
        {
            "missing" => rates[1..],
            "twice" => [.. rates[1..], rates[2]],
            "a third" => RulesWith(MovementClass.Infantry, Ground.Flat, 1.3),
            _ => RulesWith(MovementClass.Infantry, Ground.Flat, 21),
        };

        using var response = await SaveTableAsync(scenario, sent);

        await response.AssertValidationProblemAsync(
            string.Equals(wrong, "too many", StringComparison.Ordinal) ? "rates[2].hexes" : "rates"
        );
    }

    [Fact]
    public async Task ResetMovementTable_GoesBackToTheRules()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var saved = await SaveTableAsync(
            scenario,
            RulesWith(MovementClass.Slow, Ground.Flat, 2)
        );

        using var reset = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/movement", UriKind.Relative),
                Token
            );

        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.True(
            (
                await scenario
                    .As(Role.Umpire)
                    .GetAsAsync<MovementTableResponse>(
                        $"/api/campaigns/{scenario.CampaignId}/movement"
                    )
            )!.Rules
        );
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task SaveMovementTable_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SaveTableAsync(
            scenario,
            RulesWith(MovementClass.Infantry, Ground.Flat, 3),
            role
        );

        Assert.Equal(expected, response.StatusCode);
    }
}
