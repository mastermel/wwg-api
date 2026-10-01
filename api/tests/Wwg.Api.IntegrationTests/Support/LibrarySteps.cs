using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Library;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// The library (decision 0015) through the API: an editor (a Manager or Admin) fills it, and an
/// Umpire chooses an army's factions and adds its units from them.
/// </summary>
internal static class LibrarySteps
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    public static async Task<Guid> CreateFactionAsync(
        HttpClient editor,
        string name = "French",
        Nation nation = Nation.France
    )
    {
        using var response = await editor.PostAsJsonAsync(
            new Uri("/api/factions", UriKind.Relative),
            new SaveFactionRequest(name, nation),
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsAsync<FactionResponse>())?.Id
            ?? throw new InvalidOperationException("No faction.");
    }

    public static async Task<Guid> CreateUnitAsync(
        HttpClient editor,
        Guid factionId,
        string name,
        UnitType type = UnitType.LineInfantry,
        int fightingFactor = 5,
        int points = 20
    )
    {
        using var response = await editor.PostAsJsonAsync(
            new Uri($"/api/factions/{factionId}/units", UriKind.Relative),
            new SaveUnitRequest(name, type, fightingFactor, points),
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadAsAsync<UnitResponse>())?.Id
            ?? throw new InvalidOperationException("No unit.");
    }

    /// <summary>Has the army take units from these factions too, keeping the rest as they are.</summary>
    public static async Task ChooseFactionsAsync(
        HttpClient umpire,
        Guid armyId,
        params Guid[] factionIds
    )
    {
        var army =
            await umpire.GetAsAsync<ArmyResponse>($"/api/armies/{armyId}")
            ?? throw new InvalidOperationException("No army.");
        using var response = await umpire.PutAsJsonAsync(
            new Uri($"/api/armies/{armyId}", UriKind.Relative),
            new UpdateArmyRequest(
                army.Name,
                army.Side.Id,
                army.Color,
                army.Nation,
                [.. army.Factions.Select(f => f.Id).Union(factionIds)]
            ),
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    public static Task<HttpResponseMessage> AddAsync(
        HttpClient client,
        Guid armyId,
        params Guid[] unitIds
    ) =>
        client.PostAsJsonAsync(
            new Uri($"/api/armies/{armyId}/units", UriKind.Relative),
            new AddArmyUnitsRequest(unitIds),
            CancellationToken
        );

    /// <summary>
    /// A new unit in the scenario's faction, added to the army (the scenario's unless given) by
    /// the Umpire: its army unit's ID.
    /// </summary>
    public static async Task<Guid> AddUnitAsync(
        CampaignScenario scenario,
        string name,
        UnitType type = UnitType.LineInfantry,
        int fightingFactor = 5,
        int points = 20,
        Guid? armyId = null
    )
    {
        var army = armyId ?? scenario.ArmyId;
        var unitId = await CreateUnitAsync(
            scenario.As(Role.Admin),
            scenario.FactionId,
            name,
            type,
            fightingFactor,
            points
        );
        await ChooseFactionsAsync(scenario.As(Role.Umpire), army, scenario.FactionId);
        using var response = await AddAsync(scenario.As(Role.Umpire), army, unitId);
        response.EnsureSuccessStatusCode();
        var added = await response.Content.ReadAsAsync<List<ArmyUnitResponse>>();
        return added is [{ Id: var id }] ? id : throw new InvalidOperationException("No unit.");
    }
}
