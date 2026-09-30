using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Library;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The library of factions and units shared by every campaign, and Managers (decision 0015).</summary>
public sealed class LibraryTests : ApiTest
{
    private static readonly SaveFactionRequest French = new("French", Nation.France);

    private static readonly SaveUnitRequest Guard = new(
        "Imperial Guard",
        UnitType.LineInfantry,
        7,
        40
    );

    private static async Task<FactionResponse> CreateFactionAsync(
        HttpClient client,
        SaveFactionRequest? faction = null
    )
    {
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/factions", UriKind.Relative),
            faction ?? French,
            CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadAsAsync<FactionResponse>())!; // Asserted just above.
    }

    private static async Task<UnitResponse> CreateUnitAsync(
        HttpClient client,
        Guid factionId,
        SaveUnitRequest? unit = null
    )
    {
        using var response = await client.PostAsJsonAsync(
            new Uri($"/api/factions/{factionId}/units", UriKind.Relative),
            unit ?? Guard,
            CancellationToken
        );
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadAsAsync<UnitResponse>())!; // Asserted just above.
    }

    [Fact]
    public async Task CreateFaction_ByAManager_IsInTheLibraryForEveryone()
    {
        using var manager = await CreateManagerClientAsync();
        using var player = await CreateUserClientAsync();

        var faction = await CreateFactionAsync(manager);

        var listed = await player.GetAsAsync<List<FactionSummary>>("/api/factions");
        Assert.Equal([new FactionSummary(faction.Id, "French", Nation.France, 0)], listed);
    }

    [Fact]
    public async Task CreateFaction_ByAnAdmin_IsAllowed()
    {
        using var admin = await CreateAdminClientAsync();

        await CreateFactionAsync(admin);
    }

    [Fact]
    public async Task CreateFaction_ByAnUmpire_Returns403()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(new Uri("/api/factions", UriKind.Relative), French, CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListFactions_SignedOut_Returns401()
    {
        using var response = await Client.GetAsync(
            new Uri("/api/factions", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateFaction_Duplicate_IsAllowed()
    {
        using var manager = await CreateManagerClientAsync();

        await CreateFactionAsync(manager);
        await CreateFactionAsync(manager);

        Assert.Equal(2, (await manager.GetAsAsync<List<FactionSummary>>("/api/factions"))?.Count);
    }

    [Fact]
    public async Task CreateFaction_NoName_IsAValidationError()
    {
        using var manager = await CreateManagerClientAsync();

        using var response = await manager.PostAsJsonAsync(
            new Uri("/api/factions", UriKind.Relative),
            new SaveFactionRequest("  ", Nation.None),
            CancellationToken
        );

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task GetFaction_WithUnits_ListsThemByName()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);
        await CreateUnitAsync(manager, faction.Id, Guard with { Name = "Old Guard" });
        await CreateUnitAsync(manager, faction.Id, Guard with { Name = "Chasseurs" });

        var loaded = await manager.GetAsAsync<FactionResponse>($"/api/factions/{faction.Id}");

        Assert.Equal(["Chasseurs", "Old Guard"], loaded!.Units.Select(u => u.Name).ToList()); // Found above.
    }

    [Fact]
    public async Task UpdateFaction_ByAManager_RenamesIt()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);

        using var response = await manager.PutAsJsonAsync(
            new Uri($"/api/factions/{faction.Id}", UriKind.Relative),
            new SaveFactionRequest("Grande Armée", Nation.France),
            CancellationToken
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal(
            "Grande Armée",
            (await manager.GetAsAsync<FactionResponse>($"/api/factions/{faction.Id}"))?.Name
        );
    }

    [Fact]
    public async Task DeleteFaction_WithUnits_Returns409()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);
        await CreateUnitAsync(manager, faction.Id);

        using var response = await manager.DeleteAsync(
            new Uri($"/api/factions/{faction.Id}", UriKind.Relative),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeleteFaction_Empty_DeletesIt()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);

        using var response = await manager.DeleteAsync(
            new Uri($"/api/factions/{faction.Id}", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty((await manager.GetAsAsync<List<FactionSummary>>("/api/factions"))!);
    }

    [Fact]
    public async Task UpdateUnit_ByAManager_ChangesIt()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);
        var unit = await CreateUnitAsync(manager, faction.Id);

        using var response = await manager.PutAsJsonAsync(
            new Uri($"/api/units/{unit.Id}", UriKind.Relative),
            Guard with
            {
                FightingFactor = 8,
            },
            CancellationToken
        );

        var saved = await response.Content.ReadAsAsync<UnitResponse>();
        Assert.Equal(8, saved?.FightingFactor);
    }

    [Fact]
    public async Task CreateUnit_FightingFactorOutOfRange_IsAValidationError()
    {
        using var manager = await CreateManagerClientAsync();
        var faction = await CreateFactionAsync(manager);

        using var response = await manager.PostAsJsonAsync(
            new Uri($"/api/factions/{faction.Id}/units", UriKind.Relative),
            Guard with
            {
                FightingFactor = 10,
            },
            CancellationToken
        );

        await response.AssertValidationProblemAsync("fightingFactor");
    }

    [Fact]
    public async Task DeleteUnit_ByAPlayer_Returns403()
    {
        using var manager = await CreateManagerClientAsync();
        using var player = await CreateUserClientAsync();
        var faction = await CreateFactionAsync(manager);
        var unit = await CreateUnitAsync(manager, faction.Id);

        using var response = await player.DeleteAsync(
            new Uri($"/api/units/{unit.Id}", UriKind.Relative),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SetManager_ByAnAdmin_LetsTheUserEditTheLibraryAtOnce()
    {
        using var admin = await CreateAdminClientAsync();
        using var user = await CreateUserClientAsync("bob@example.com");
        var bob = (await user.GetAsAsync<MeResponse>("/api/me"))!;
        using var refused = await user.PostAsJsonAsync(
            new Uri("/api/factions", UriKind.Relative),
            French,
            CancellationToken
        );

        using var granted = await admin.PutAsJsonAsync(
            new Uri($"/api/admin/users/{bob.Id}/manager", UriKind.Relative),
            new SetManagerRequest(true),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
        Assert.True((await user.GetAsAsync<MeResponse>("/api/me"))?.IsManager);
        Assert.True((await admin.GetAsAsync<UserDetails>($"/api/admin/users/{bob.Id}"))?.IsManager);
        // The same token as before: the role is read from the database.
        await CreateFactionAsync(user);
    }

    [Fact]
    public async Task SetManager_Off_StopsTheUserEditingAtOnce()
    {
        using var admin = await CreateAdminClientAsync();
        using var manager = await CreateManagerClientAsync();
        var me = (await manager.GetAsAsync<MeResponse>("/api/me"))!;

        using var removed = await admin.PutAsJsonAsync(
            new Uri($"/api/admin/users/{me.Id}/manager", UriKind.Relative),
            new SetManagerRequest(false),
            CancellationToken
        );
        using var refused = await manager.PostAsJsonAsync(
            new Uri("/api/factions", UriKind.Relative),
            French,
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task SetManager_ByAManager_Returns403()
    {
        using var manager = await CreateManagerClientAsync();
        var me = (await manager.GetAsAsync<MeResponse>("/api/me"))!;

        using var response = await manager.PutAsJsonAsync(
            new Uri($"/api/admin/users/{me.Id}/manager", UriKind.Relative),
            new SetManagerRequest(true),
            CancellationToken
        );

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
