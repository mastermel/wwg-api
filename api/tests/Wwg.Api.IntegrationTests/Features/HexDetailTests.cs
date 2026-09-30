using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>A hex's actual terrain, found by the Umpire's dice (decision 0016; the rules, p. 57).</summary>
public sealed class HexDetailTests : ApiTest
{
    private readonly FixedDice _dice = new();

    private static readonly HexFeatures NoFeatures = new(
        false,
        false,
        false,
        false,
        false,
        false,
        false
    );

    public HexDetailTests()
    {
        App.TestServices.Add(services =>
            services.Replace(ServiceDescriptor.Singleton<IDice>(_dice))
        );
    }

    private static Uri DetailsUri(CampaignScenario scenario, string path = "") =>
        new($"/api/campaigns/{scenario.CampaignId}/grid/details{path}", UriKind.Relative);

    private static async Task<CampaignScenario> WithAreaAsync(CampaignScenario scenario)
    {
        await TurnSteps.SetAreaAsync(scenario);
        return scenario;
    }

    private static Task<HttpResponseMessage> RollAsync(
        CampaignScenario scenario,
        RollHexDetailRequest? request = null,
        int q = 0,
        int r = 0,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                DetailsUri(scenario, $"/{q}/{r}/roll"),
                request ?? new RollHexDetailRequest(null, false, false),
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> UpdateAsync(
        CampaignScenario scenario,
        UpdateHexDetailRequest request,
        int q = 0,
        int r = 0
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                DetailsUri(scenario, $"/{q}/{r}"),
                request,
                TestContext.Current.CancellationToken
            );

    private static UpdateHexDetailRequest Shown(IReadOnlyList<Guid> armies, bool all = false) =>
        new(
            DetailRelief.Flat,
            NoFeatures,
            DominantFeature.None,
            Favorability.NotRolled,
            null,
            armies,
            all
        );

    private static async Task SetCellAsync(CampaignScenario scenario, Terrain terrain, bool forest)
    {
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/grid/cells/0/0", UriKind.Relative),
                new UpdateHexCellRequest(terrain, forest, HexSettlement.None),
                TestContext.Current.CancellationToken
            );
        response.EnsureSuccessStatusCode();
    }

    private static async Task<List<HexDetailResponse>> ListAsync(
        CampaignScenario scenario,
        Role role
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<HexDetailResponse>>(DetailsUri(scenario).ToString())
        )!;

    [Fact]
    public async Task RollHexDetail_OnAFlatHex_ReadsTheTable()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        _dice.WillRoll(4, 1);

        using var response = await RollAsync(
            scenario,
            new RollHexDetailRequest(scenario.ArmyId, false, false)
        );

        var expected = new HexDetailResponse(
            0,
            0,
            DetailRelief.Rolling,
            NoFeatures with
            {
                Village = true,
                Woods = true,
            },
            DominantFeature.SmallCastle,
            Favorability.NotRolled,
            new HexDice(4, 1, null),
            scenario.ArmyId,
            [],
            false
        );
        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        // Records compare lists by reference: the list on its own.
        Assert.Equal(expected, detail with { ShownToArmyIds = expected.ShownToArmyIds });
        Assert.Empty(detail.ShownToArmyIds);
    }

    [Theory]
    [InlineData(Terrain.HighHill, false, 6, 7, DetailRelief.HighHills)]
    [InlineData(Terrain.Mountain, false, 3, 5, DetailRelief.Rolling)]
    [InlineData(Terrain.LowHill, true, 3, 4, DetailRelief.Rolling)]
    [InlineData(Terrain.Flat, true, 5, 6, DetailRelief.Hilly)]
    public async Task RollHexDetail_OnRoughGround_AddsTheModifier(
        Terrain terrain,
        bool forest,
        int red,
        int counted,
        DetailRelief relief
    )
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        await SetCellAsync(scenario, terrain, forest);
        _dice.WillRoll(red, 2);

        using var response = await RollAsync(scenario);

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Equal((counted, relief), (detail.Dice!.Red, detail.Relief));
    }

    [Fact]
    public async Task RollHexDetail_OneOffAFlatHex_CanFindScrub()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        _dice.WillRoll(1, 6);

        using var response = await RollAsync(scenario, new RollHexDetailRequest(null, false, true));

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Equal(
            (0, DetailRelief.Flat, NoFeatures with { Scrub = true }, DominantFeature.None),
            (detail.Dice!.Red, detail.Relief, detail.Features, detail.Dominant)
        );
    }

    [Fact]
    public async Task RollHexDetail_OneOffAHill_IsAValidationError()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        await SetCellAsync(scenario, Terrain.LowHill, false);

        using var response = await RollAsync(scenario, new RollHexDetailRequest(null, false, true));

        await response.AssertValidationProblemAsync("flatMinusOne");
    }

    [Theory]
    [InlineData(1, Favorability.Favorable)]
    [InlineData(4, Favorability.Neutral)]
    [InlineData(6, Favorability.Unfavorable)]
    public async Task RollHexDetail_WithFavorability_RollsTheGreenDie(
        int green,
        Favorability expected
    )
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        _dice.WillRoll(2, 3, green);

        using var response = await RollAsync(scenario, new RollHexDetailRequest(null, true, false));

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Equal(
            (new HexDice(2, 3, green), expected, DominantFeature.WeakFarmhouse),
            (detail.Dice, detail.Favorability, detail.Dominant)
        );
    }

    [Fact]
    public async Task RollHexDetail_Again_ReplacesTheRollAndKeepsWhoSeesIt()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var shown = await UpdateAsync(scenario, Shown([scenario.ArmyId]));
        _dice.WillRoll(5, 5);

        using var response = await RollAsync(scenario);

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Equal(DetailRelief.Rolling, detail.Relief);
        Assert.Equal(DominantFeature.StrongFarmhouse, detail.Dominant);
        Assert.Equal([scenario.ArmyId], detail.ShownToArmyIds);
    }

    [Fact]
    public async Task RollHexDetail_ForAnotherCampaignsArmy_IsAValidationError()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await RollAsync(
            scenario,
            new RollHexDetailRequest(Guid.CreateVersion7(), false, false)
        );

        await response.AssertValidationProblemAsync("forArmyId");
    }

    [Fact]
    public async Task RollHexDetail_OutsideTheGrid_Returns404()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await RollAsync(scenario, q: 500);

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task RollHexDetail_NoArea_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RollAsync(scenario);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task UpdateHexDetail_WithoutARoll_HasNoDice()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await UpdateAsync(
            scenario,
            Shown([]) with
            {
                Relief = DetailRelief.Hilly,
                Features = NoFeatures with { Farms = true, Streams = true },
                Dominant = DominantFeature.WeakFarmhouse,
            }
        );

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Null(detail.Dice);
        Assert.Equal(
            (DetailRelief.Hilly, DominantFeature.WeakFarmhouse),
            (detail.Relief, detail.Dominant)
        );
    }

    [Fact]
    public async Task UpdateHexDetail_ChangingARoll_KeepsItsDice()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        _dice.WillRoll(4, 1);
        using var rolled = await RollAsync(scenario);

        using var response = await UpdateAsync(
            scenario,
            Shown([]) with
            {
                Dominant = DominantFeature.None,
            }
        );

        var detail = (await response.Content.ReadAsAsync<HexDetailResponse>())!;
        Assert.Equal(new HexDice(4, 1, null), detail.Dice);
        Assert.Equal(DominantFeature.None, detail.Dominant);
    }

    [Fact]
    public async Task UpdateHexDetail_ShownToAnotherCampaignsArmy_IsAValidationError()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await UpdateAsync(scenario, Shown([Guid.CreateVersion7()]));

        await response.AssertValidationProblemAsync("shownToArmyIds");
    }

    [Fact]
    public async Task ListHexDetails_ShowsEachMemberWhatsShownToThem()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        // (0, 0) shown to the scenario's army, (1, 0) to all, (0, 1) to no one yet.
        using var toArmy = await UpdateAsync(
            scenario,
            Shown([scenario.ArmyId]) with
            {
                ForArmyId = scenario.ArmyId,
            }
        );
        using var toAll = await UpdateAsync(scenario, Shown([], all: true), q: 1);
        using var hidden = await UpdateAsync(scenario, Shown([]), r: 1);

        var umpire = await ListAsync(scenario, Role.Umpire);
        var commander = await ListAsync(scenario, Role.Commander);
        var player = await ListAsync(scenario, Role.Player);

        Assert.Equal(3, umpire.Count);
        Assert.Equal([scenario.ArmyId], umpire.Single(d => d is { Q: 0, R: 0 }).ShownToArmyIds);
        Assert.Equal([(0, 0), (1, 0)], commander.Select(d => (d.Q, d.R)));
        // Who asked, and who else sees it, are the Umpire's to know.
        Assert.All(commander, d => Assert.Null(d.ForArmyId));
        Assert.All(commander, d => Assert.Empty(d.ShownToArmyIds));
        Assert.Equal([(1, 0)], player.Select(d => (d.Q, d.R)));
    }

    [Fact]
    public async Task DeleteHexDetail_ForgetsIt()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var rolled = await RollAsync(scenario);

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(DetailsUri(scenario, "/0/0"), CancellationToken);
        using var again = await scenario
            .As(Role.Umpire)
            .DeleteAsync(DetailsUri(scenario, "/0/0"), CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await again.AssertProblemAsync(HttpStatusCode.NotFound);
        Assert.Empty(await ListAsync(scenario, Role.Umpire));
    }

    [Fact]
    public async Task UpdateCampaignMap_NewHexSizeWhileSettingUp_ForgetsTheDetails()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var shown = await UpdateAsync(scenario, Shown([scenario.ArmyId]));

        await TurnSteps.SetAreaAsync(scenario, hexSize: 3000);

        Assert.Empty(await ListAsync(scenario, Role.Umpire));
    }

    [Fact]
    public async Task DeleteArmy_ThatAsked_KeepsWhatWasFound()
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());
        using var rolled = await RollAsync(
            scenario,
            new RollHexDetailRequest(scenario.ArmyId, false, false)
        );
        using var shown = await UpdateAsync(
            scenario,
            Shown([scenario.ArmyId]) with
            {
                ForArmyId = scenario.ArmyId,
            }
        );

        using var deleted = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );

        deleted.EnsureSuccessStatusCode();
        var detail = Assert.Single(await ListAsync(scenario, Role.Umpire));
        Assert.Null(detail.ForArmyId);
        Assert.Empty(detail.ShownToArmyIds);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task RollHexDetail_ByRole_TheUmpire(Role role, HttpStatusCode expected)
    {
        using var scenario = await WithAreaAsync(await CreateCampaignScenarioAsync());

        using var response = await RollAsync(scenario, role: role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListHexDetails_ByRole_EveryMember(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(DetailsUri(scenario), CancellationToken);

        Assert.Equal(expected, response.StatusCode);
    }
}
