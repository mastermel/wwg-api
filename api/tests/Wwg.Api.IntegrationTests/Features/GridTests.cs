using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The terrain on a campaign's hex grid (decision 0014).</summary>
public sealed class GridTests : ApiTest
{
    private static readonly InferredHexCell Hill = new(
        0,
        0,
        Terrain.LowHill,
        false,
        Settlement.None
    );

    private static readonly InferredHexEdge Road = new(
        0,
        0,
        EdgeSide.N,
        RoadQuality.Good,
        false,
        false
    );

    private static Uri GridUri(CampaignScenario scenario, string path = "") =>
        new($"/api/campaigns/{scenario.CampaignId}/grid{path}", UriKind.Relative);

    private static async Task<CampaignScenario> WithAreaAsync(CampaignScenario scenario)
    {
        await TurnSteps.SetAreaAsync(scenario);
        return scenario;
    }

    private static Task<HttpResponseMessage> SaveAsync(
        CampaignScenario scenario,
        IReadOnlyList<InferredHexCell> cells,
        IReadOnlyList<InferredHexEdge>? edges = null,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                GridUri(scenario),
                new SaveCampaignGridRequest(cells, edges ?? []),
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> SetCellAsync(
        CampaignScenario scenario,
        int q,
        int r,
        UpdateHexCellRequest cell,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                GridUri(scenario, $"/cells/{q}/{r}"),
                cell,
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> SetEdgeAsync(
        CampaignScenario scenario,
        int q,
        int r,
        string side,
        UpdateHexEdgeRequest edge
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                GridUri(scenario, $"/edges/{q}/{r}/{side}"),
                edge,
                TestContext.Current.CancellationToken
            );

    private static async Task<CampaignGridResponse> GridAsync(CampaignScenario scenario) =>
        (
            await scenario
                .As(Role.Player)
                .GetAsAsync<CampaignGridResponse>(GridUri(scenario).ToString())
        )!;

    /// <summary>A hex south of the grid, whose N edge is on the grid's southern border.</summary>
    private static Hex BelowTheGrid()
    {
        var grid = new HexGrid(TurnSteps.Area, CampaignMap.DefaultHexSize);
        var r = 0;
        while (grid.Contains(new Hex(0, r)))
        {
            r++;
        }

        return new Hex(0, r);
    }

    [Fact]
    public async Task GetCampaignGrid_NothingSaved_IsEmpty()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var grid = await GridAsync(scenario);

        Assert.Empty(grid.Cells);
        Assert.Empty(grid.Edges);
    }

    [Fact]
    public async Task SaveCampaignGrid_Inferred_StoresOnlyWhatHasSomethingOnIt()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SaveAsync(
            scenario,
            [
                Hill,
                new(1, 0, Terrain.Flat, true, Settlement.None),
                new(0, 1, Terrain.Flat, false, Settlement.None),
            ],
            [Road, new(0, 0, EdgeSide.NE, RoadQuality.None, false, false)]
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var grid = await GridAsync(scenario);
        Assert.Equal(
            [
                new HexCellResponse(0, 0, Terrain.LowHill, false, Settlement.None, false),
                new HexCellResponse(1, 0, Terrain.Flat, true, Settlement.None, false),
            ],
            grid.Cells
        );
        Assert.Equal(
            [new HexEdgeResponse(0, 0, EdgeSide.N, RoadQuality.Good, false, false, false)],
            grid.Edges
        );
    }

    [Fact]
    public async Task SaveCampaignGrid_Again_ReplacesTheInferredButKeepsTheUmpiresEdits()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var first = await SaveAsync(
            scenario,
            [Hill, new(1, 0, Terrain.Mountain, false, Settlement.None)],
            [Road]
        );
        using var cell = await SetCellAsync(
            scenario,
            1,
            0,
            new UpdateHexCellRequest(Terrain.Flat, false, Settlement.WalledCity)
        );
        using var edge = await SetEdgeAsync(
            scenario,
            0,
            0,
            "NE",
            new UpdateHexEdgeRequest(RoadQuality.Poor, true, true)
        );

        using var response = await SaveAsync(
            scenario,
            [
                new(1, 0, Terrain.Water, false, Settlement.None),
                new(-1, 0, Terrain.HighHill, false, Settlement.None),
            ],
            [Road with { Side = EdgeSide.NE, Road = RoadQuality.Good }]
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var grid = await GridAsync(scenario);
        Assert.Equal(
            [
                new HexCellResponse(-1, 0, Terrain.HighHill, false, Settlement.None, false),
                new HexCellResponse(1, 0, Terrain.Flat, false, Settlement.WalledCity, true),
            ],
            grid.Cells
        );
        Assert.Equal(
            [new HexEdgeResponse(0, 0, EdgeSide.NE, RoadQuality.Poor, true, true, true)],
            grid.Edges
        );
    }

    [Fact]
    public async Task SaveCampaignGrid_NoArea_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SaveAsync(scenario, [Hill]);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task SaveCampaignGrid_AnEdgeOnTheBorder_IsKept()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        var below = BelowTheGrid();

        using var response = await SaveAsync(
            scenario,
            [],
            [Road with { Q = below.Q, R = below.R }]
        );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single((await GridAsync(scenario)).Edges);
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("twice")]
    public async Task SaveCampaignGrid_BadCell_IsAValidationError(string problem)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        InferredHexCell[] cells = string.Equals(problem, "outside", StringComparison.Ordinal)
            ? [Hill with { Q = 500 }]
            : [Hill, Hill];

        using var response = await SaveAsync(scenario, cells);

        await response.AssertValidationProblemAsync("cells");
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("twice")]
    [InlineData("bridge without a river")]
    public async Task SaveCampaignGrid_BadEdge_IsAValidationError(string problem)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        var below = BelowTheGrid();
        InferredHexEdge[] edges = problem switch
        {
            "outside" => [Road with { Q = below.Q, R = below.R + 1 }],
            "twice" => [Road, Road],
            _ => [Road with { Bridge = true }],
        };

        using var response = await SaveAsync(scenario, [], edges);

        await response.AssertValidationProblemAsync("edges");
    }

    [Fact]
    public async Task SaveCampaignGrid_UndefinedTerrainNumber_IsAValidationError()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                GridUri(scenario),
                new
                {
                    cells = new[]
                    {
                        new
                        {
                            q = 0,
                            r = 0,
                            terrain = 99,
                            forest = false,
                            settlement = "None",
                        },
                    },
                    edges = Array.Empty<object>(),
                },
                TestJson.Options,
                CancellationToken
            );

        await response.AssertValidationProblemAsync("cells[0].terrain");
    }

    [Fact]
    public async Task UpdateHexCell_OutsideTheGrid_Returns404()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SetCellAsync(
            scenario,
            500,
            0,
            new UpdateHexCellRequest(Terrain.Water, false, Settlement.None)
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateHexCell_NoArea_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await SetCellAsync(
            scenario,
            0,
            0,
            new UpdateHexCellRequest(Terrain.Water, false, Settlement.None)
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateHexCell_ToFlat_IsKeptAsTheUmpiresChoice()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var saved = await SaveAsync(scenario, [Hill]);

        using var response = await SetCellAsync(
            scenario,
            0,
            0,
            new UpdateHexCellRequest(Terrain.Flat, false, Settlement.None)
        );
        using var again = await SaveAsync(scenario, [Hill]);

        Assert.Equal(
            new HexCellResponse(0, 0, Terrain.Flat, false, Settlement.None, true),
            await response.Content.ReadAsAsync<HexCellResponse>()
        );
        Assert.Equal(
            [new HexCellResponse(0, 0, Terrain.Flat, false, Settlement.None, true)],
            (await GridAsync(scenario)).Cells
        );
    }

    [Fact]
    public async Task UpdateHexEdge_BridgeWithoutARiver_IsAValidationError()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SetEdgeAsync(
            scenario,
            0,
            0,
            "SE",
            new UpdateHexEdgeRequest(RoadQuality.Good, false, true)
        );

        await response.AssertValidationProblemAsync("bridge");
    }

    [Theory]
    [InlineData("S", HttpStatusCode.BadRequest)]
    [InlineData("7", HttpStatusCode.NotFound)]
    public async Task UpdateHexEdge_NotAStoredSide_IsRefused(string side, HttpStatusCode expected)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SetEdgeAsync(
            scenario,
            0,
            0,
            side,
            new UpdateHexEdgeRequest(RoadQuality.Good, false, false)
        );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task UpdateHexEdge_OnTheBorder_IsSaved()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        var below = BelowTheGrid();

        using var response = await SetEdgeAsync(
            scenario,
            below.Q,
            below.R,
            "N",
            new UpdateHexEdgeRequest(RoadQuality.None, true, false)
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateCampaignMap_NewHexSizeWhileSettingUp_ClearsTheTerrain()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var saved = await SaveAsync(scenario, [Hill], [Road]);
        using var set = await SetCellAsync(
            scenario,
            1,
            0,
            new UpdateHexCellRequest(Terrain.Water, false, Settlement.None)
        );

        await TurnSteps.SetAreaAsync(scenario, hexSize: 3000);

        var grid = await GridAsync(scenario);
        Assert.Empty(grid.Cells);
        Assert.Empty(grid.Edges);
    }

    [Fact]
    public async Task UpdateCampaignMap_SameGrid_KeepsTheTerrain()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var saved = await SaveAsync(scenario, [Hill]);

        await TurnSteps.SetAreaAsync(scenario);

        Assert.Single((await GridAsync(scenario)).Cells);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task GetCampaignGrid_ByRole_EveryMember(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario.As(role).GetAsync(GridUri(scenario), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task SaveCampaignGrid_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SaveAsync(scenario, [Hill], role: role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task UpdateHexCell_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await SetCellAsync(
            scenario,
            0,
            0,
            new UpdateHexCellRequest(Terrain.Water, false, Settlement.None),
            role
        );

        Assert.Equal(expected, response.StatusCode);
    }
}
