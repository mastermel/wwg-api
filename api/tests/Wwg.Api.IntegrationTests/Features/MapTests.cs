using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>A campaign's map settings (DESIGN.md §5.1, §3.13), and their §5.2 rows.</summary>
public sealed class MapTests : ApiTest
{
    /// <summary>Waterloo and around.</summary>
    private static readonly MapBounds Waterloo = new(4.2, 50.6, 4.6, 50.8);

    private static UpdateCampaignMapRequest Request(
        MapBounds? bounds = null,
        string language = "fr",
        int hexSize = 3000
    ) =>
        new(
            bounds ?? Waterloo,
            language,
            DistanceUnit.Kilometres,
            new MapLayers(
                Roads: true,
                Places: true,
                Water: true,
                Forests: false,
                Hills: true,
                Contours: true,
                Grid: false
            ),
            hexSize
        );

    private static Task<HttpResponseMessage> PutAsync(
        CampaignScenario scenario,
        UpdateCampaignMapRequest request,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/map", UriKind.Relative),
                request,
                TestContext.Current.CancellationToken
            );

    private static Task<CampaignMapResponse?> GetAsync(CampaignScenario scenario, Role role) =>
        scenario
            .As(role)
            .GetAsAsync<CampaignMapResponse>($"/api/campaigns/{scenario.CampaignId}/map");

    [Fact]
    public async Task GetCampaignMap_NothingSaved_ReturnsTheDefaultsWithNoBounds()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var map = await GetAsync(scenario, Role.Player);

        Assert.Null(map?.Bounds);
        Assert.Equal(("en", DistanceUnit.Miles), (map?.LabelLanguage, map?.DistanceUnit));
        Assert.Equal(CampaignMaps.DefaultLayers, map?.Layers);
        Assert.Equal(4828, map?.HexSize);
    }

    [Fact]
    public async Task UpdateCampaignMap_ByTheUmpire_SavesEverythingForEveryone()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var map = await GetAsync(scenario, Role.Player);
        Assert.Equal(Waterloo, map?.Bounds);
        Assert.Equal(("fr", DistanceUnit.Kilometres), (map?.LabelLanguage, map?.DistanceUnit));
        Assert.False(map?.Layers.Forests);
        Assert.True(map?.Layers.Contours);
        Assert.False(map?.Layers.Grid);
        Assert.Equal(3000, map?.HexSize);
    }

    [Fact]
    public async Task UpdateCampaignMap_Twice_ChangesTheSavedSettings()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var first = await PutAsync(scenario, Request());

        using var second = await PutAsync(
            scenario,
            Request(language: "local", hexSize: 5000) with
            {
                Bounds = null,
            }
        );

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var map = await GetAsync(scenario, Role.Umpire);
        Assert.Null(map?.Bounds);
        Assert.Equal(("local", 5000), (map?.LabelLanguage, map?.HexSize));
    }

    [Theory]
    [InlineData(4.6, 50.6, 4.2, 50.8, "bounds.east")]
    [InlineData(4.2, 50.8, 4.6, 50.6, "bounds.north")]
    [InlineData(-181, 50.6, 4.6, 50.8, "bounds.west")]
    [InlineData(4.2, 50.6, 4.6, 89, "bounds.north")]
    public async Task UpdateCampaignMap_BadBounds_IsAValidationError(
        double west,
        double south,
        double east,
        double north,
        string field
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(
            scenario,
            Request(new MapBounds(west, south, east, north))
        );

        await response.AssertValidationProblemAsync(field);
    }

    [Fact]
    public async Task UpdateCampaignMap_UnknownLanguage_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request(language: "klingon"));

        await response.AssertValidationProblemAsync("labelLanguage");
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task GetCampaignMap_ByRole_ReturnsExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/map", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateCampaignMap_ByRole_ReturnsExpectedStatus(
        Role role,
        HttpStatusCode expected
    )
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request(), role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampaignMap_HexSize_IsSaved()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request(hexSize: 8000));

        response.EnsureSuccessStatusCode();
        Assert.Equal(8000, (await GetAsync(scenario, Role.Player))?.HexSize);
    }

    [Theory]
    [InlineData(499)]
    [InlineData(50_001)]
    public async Task UpdateCampaignMap_HexSizeOutOfRange_IsAValidationError(int hexSize)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request(hexSize: hexSize));

        await response.AssertValidationProblemAsync("hexSize");
    }

    [Fact]
    public async Task UpdateCampaignMap_AnotherHexSizeOnceStarted_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);

        using var response = await PutAsync(
            scenario,
            Request(TurnSteps.Area, hexSize: CampaignMap.DefaultHexSize + 1)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateCampaignMap_OtherBoundsOnceStarted_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);

        using var response = await PutAsync(
            scenario,
            Request(new MapBounds(4.1, 50.6, 4.6, 50.8), hexSize: CampaignMap.DefaultHexSize)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateCampaignMap_OnlyLayersAndLanguageOnceStarted_Saves()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);

        using var response = await PutAsync(
            scenario,
            Request(TurnSteps.Area, language: "de", hexSize: CampaignMap.DefaultHexSize)
        );

        response.EnsureSuccessStatusCode();
        Assert.Equal("de", (await GetAsync(scenario, Role.Player))?.LabelLanguage);
    }

    [Fact]
    public async Task UpdateCampaignMap_AnotherHexSizeWhileSettingUp_MovesThePlacements()
    {
        var resnap = HexGridFigures.Resnap;
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetAreaAsync(scenario, resnap.FromHexSize);
        var units = new List<(Guid Id, HexGridFigures.ResnapHex Hex)>();
        foreach (var hex in resnap.Hexes.Where(h => new Hex(h.Q, h.R) != TurnSteps.Start))
        {
            var id = await AddUnitAsync(scenario);
            using var placed = await TurnSteps.PlaceAsync(scenario, id, new Hex(hex.Q, hex.R));
            if (placed.IsSuccessStatusCode)
            {
                units.Add((id, hex));
            }
        }

        using var response = await PutAsync(
            scenario,
            Request(TurnSteps.Area, language: "en", hexSize: resnap.ToHexSize)
        );

        response.EnsureSuccessStatusCode();
        var positions =
            await scenario
                .As(Role.Umpire)
                .GetAsAsync<List<UnitPosition>>(
                    $"/api/campaigns/{scenario.CampaignId}/positions?turn=0"
                )
            ?? [];
        Assert.NotEmpty(units);
        foreach (var (id, hex) in units)
        {
            var position = positions.SingleOrDefault(p => p.UnitId == id);
            if (hex.InGrid)
            {
                Assert.Equal((hex.To.Q, hex.To.R), (position?.Q, position?.R));
            }
            else
            {
                Assert.Null(position);
            }
        }
    }

    private static async Task<Guid> AddUnitAsync(CampaignScenario scenario)
    {
        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/units", UriKind.Relative),
                new CreateArmyUnitRequest("Brigade", UnitType.LineInfantry, 3, 10),
                CancellationToken
            );
        return (await created.Content.ReadAsAsync<ArmyUnitResponse>())?.Id
            ?? throw new InvalidOperationException("No unit.");
    }
}
