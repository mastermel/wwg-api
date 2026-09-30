using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>Getting a scenario's campaign through setup and its turns, through the API.</summary>
internal static class TurnSteps
{
    /// <summary>Waterloo and around.</summary>
    public static readonly MapBounds Area = new(4.2, 50.6, 4.6, 50.8);

    /// <summary>Where the scenario's unit is placed: the hex in the area's middle.</summary>
    public static readonly Hex Start = new(0, 0);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>A Move along these hexes, in order.</summary>
    public static GiveOrderRequest Move(params Hex[] path) => new(OrderKind.Move, path);

    public static readonly GiveOrderRequest Hold = new(OrderKind.Hold, null);

    /// <summary>Sets the map's area (and its hex size: 3 miles unless given).</summary>
    public static async Task SetAreaAsync(
        CampaignScenario scenario,
        int hexSize = CampaignMap.DefaultHexSize
    )
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/map", UriKind.Relative),
                new UpdateCampaignMapRequest(
                    Area,
                    "en",
                    DistanceUnit.Kilometres,
                    CampaignMaps.DefaultLayers,
                    hexSize
                ),
                CancellationToken
            );
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> PlaceAsync(
        CampaignScenario scenario,
        Guid unitId,
        Hex? at = null,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/army-units/{unitId}/placement", UriKind.Relative),
                new PlaceUnitRequest((at ?? Start).Q, (at ?? Start).R),
                CancellationToken
            );

    public static Task<HttpResponseMessage> StartAsync(
        CampaignScenario scenario,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PostAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/start", UriKind.Relative),
                null,
                CancellationToken
            );

    /// <summary>The area set and the scenario's unit placed at <see cref="Start"/>: ready to start.</summary>
    public static async Task ReadyAsync(CampaignScenario scenario)
    {
        await SetAreaAsync(scenario);
        using var placed = await PlaceAsync(scenario, scenario.UnitId);
        placed.EnsureSuccessStatusCode();
    }

    /// <summary>Ready, and started: turn 1 is open.</summary>
    public static async Task StartedAsync(CampaignScenario scenario)
    {
        await ReadyAsync(scenario);
        using var started = await StartAsync(scenario);
        started.EnsureSuccessStatusCode();
    }

    /// <summary>The scenario's army's turns, newest first, as the Umpire sees them.</summary>
    public static async Task<List<ArmyTurnDetails>> ArmyTurnsAsync(CampaignScenario scenario) =>
        await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<ArmyTurnDetails>>($"/api/armies/{scenario.ArmyId}/turns")
        ?? [];

    /// <summary>The scenario's army's turn in the open campaign turn.</summary>
    public static async Task<ArmyTurnDetails> OpenArmyTurnAsync(CampaignScenario scenario) =>
        (await ArmyTurnsAsync(scenario)).Single(t => t.Open);

    public static Task<HttpResponseMessage> OrderAsync(
        CampaignScenario scenario,
        Guid armyTurnId,
        GiveOrderRequest order,
        Guid? unitId = null,
        Role role = Role.Commander
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri(
                    $"/api/army-turns/{armyTurnId}/orders/{unitId ?? scenario.UnitId}",
                    UriKind.Relative
                ),
                order,
                CancellationToken
            );

    /// <summary>Posts a turn action (submit, approve, send-back, revert) for an army turn.</summary>
    public static Task<HttpResponseMessage> ActAsync(
        CampaignScenario scenario,
        Guid armyTurnId,
        string action,
        Role role,
        ReviewTurnRequest? review = null
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/army-turns/{armyTurnId}/{action}", UriKind.Relative),
                review,
                CancellationToken
            );

    /// <summary>The open turn's army turn, with a Hold for the scenario's unit, submitted.</summary>
    public static async Task<ArmyTurnDetails> SubmittedAsync(CampaignScenario scenario)
    {
        var turn = await OpenArmyTurnAsync(scenario);
        using var held = await OrderAsync(scenario, turn.Id, Hold);
        held.EnsureSuccessStatusCode();
        using var submitted = await ActAsync(scenario, turn.Id, "submit", Role.Commander);
        submitted.EnsureSuccessStatusCode();
        return turn;
    }

    /// <summary>The open turn's army turn, submitted and approved.</summary>
    public static async Task<ArmyTurnDetails> CompletedAsync(CampaignScenario scenario)
    {
        var turn = await SubmittedAsync(scenario);
        using var approved = await ActAsync(scenario, turn.Id, "approve", Role.Umpire);
        approved.EnsureSuccessStatusCode();
        return turn;
    }

    public static Task<HttpResponseMessage> StartNextTurnAsync(
        CampaignScenario scenario,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PostAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/turns", UriKind.Relative),
                null,
                CancellationToken
            );
}
