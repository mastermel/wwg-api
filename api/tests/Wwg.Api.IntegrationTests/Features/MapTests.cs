using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>A campaign's map settings (DESIGN.md §5.1, §3.13), and their §5.2 rows.</summary>
public sealed class MapTests : ApiTest
{
    /// <summary>Waterloo and around.</summary>
    private static readonly MapBounds Waterloo = new(4.2, 50.6, 4.6, 50.8);

    private static List<MovementLimitDto> Limits(int metres = 10_000) =>
        [.. Enum.GetValues<UnitType>().Select(type => new MovementLimitDto(type, metres))];

    private static UpdateCampaignMapRequest Request(
        MapBounds? bounds = null,
        string language = "fr",
        IReadOnlyList<MovementLimitDto>? limits = null,
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
                Contours: true
            ),
            hexSize,
            limits ?? Limits()
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
        Assert.Equal(
            Enum.GetValues<UnitType>().Length,
            map!.MovementLimits.Select(l => l.UnitType).Distinct().Count()
        );
        Assert.Equal(
            40_000,
            map.MovementLimits.Single(l => l.UnitType == UnitType.LightCavalry).Metres
        );
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
        Assert.All(map!.MovementLimits, l => Assert.Equal(10_000, l.Metres));
    }

    [Fact]
    public async Task UpdateCampaignMap_Twice_ChangesTheSavedSettings()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var first = await PutAsync(scenario, Request());

        using var second = await PutAsync(
            scenario,
            Request(language: "local", limits: Limits(5_000)) with
            {
                Bounds = null,
            }
        );

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var map = await GetAsync(scenario, Role.Umpire);
        Assert.Null(map?.Bounds);
        Assert.Equal("local", map?.LabelLanguage);
        Assert.All(map!.MovementLimits, l => Assert.Equal(5_000, l.Metres));
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

    [Fact]
    public async Task UpdateCampaignMap_AUnitTypeMissing_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PutAsync(scenario, Request(limits: Limits().Skip(1).ToList()));

        await response.AssertValidationProblemAsync("movementLimits");
    }

    [Fact]
    public async Task UpdateCampaignMap_AUnitTypeTwice_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var limits = Limits();
        limits[0] = limits[1];

        using var response = await PutAsync(scenario, Request(limits: limits));

        await response.AssertValidationProblemAsync("movementLimits");
    }

    [Fact]
    public async Task UpdateCampaignMap_ALimitOverAThousandKilometres_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var limits = Limits();
        limits[0] = limits[0] with { Metres = 1_000_001 };

        using var response = await PutAsync(scenario, Request(limits: limits));

        await response.AssertValidationProblemAsync("movementLimits[0].metres");
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
}
