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

    /// <summary>Where the scenario's unit is placed: in the area's middle.</summary>
    public static readonly (double Latitude, double Longitude) Start = (50.7, 4.4);

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    /// <summary>Sets the map's area, and every unit type's movement limit.</summary>
    public static async Task SetAreaAsync(CampaignScenario scenario, int limitMetres = 20_000)
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
                    [
                        .. Enum.GetValues<UnitType>()
                            .Select(type => new MovementLimitDto(type, limitMetres)),
                    ]
                ),
                CancellationToken
            );
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> PlaceAsync(
        CampaignScenario scenario,
        Guid unitId,
        double latitude = 50.7,
        double longitude = 4.4,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/units/{unitId}/placement", UriKind.Relative),
                new PlaceUnitRequest(latitude, longitude),
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
    public static async Task ReadyAsync(CampaignScenario scenario, int limitMetres = 20_000)
    {
        await SetAreaAsync(scenario, limitMetres);
        using var placed = await PlaceAsync(
            scenario,
            scenario.UnitId,
            Start.Latitude,
            Start.Longitude
        );
        placed.EnsureSuccessStatusCode();
    }

    /// <summary>Ready, and started: turn 1 is open.</summary>
    public static async Task StartedAsync(CampaignScenario scenario, int limitMetres = 20_000)
    {
        await ReadyAsync(scenario, limitMetres);
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
}
