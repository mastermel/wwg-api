using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Sides;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>
/// Sides (DESIGN.md §5.1; decision 0017): exactly two a campaign, made with it, which the Umpire
/// renames; every member sees them.
/// </summary>
public sealed class SideTests : ApiTest
{
    private static Task<HttpResponseMessage> RenameAsync(
        CampaignScenario scenario,
        Guid sideId,
        string name,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/sides/{sideId}", UriKind.Relative),
                new RenameSideRequest(name),
                TestContext.Current.CancellationToken
            );

    private static async Task<List<SideResponse>> SidesAsync(
        CampaignScenario scenario,
        Role role
    ) =>
        (
            await scenario
                .As(role)
                .GetAsAsync<List<SideResponse>>($"/api/campaigns/{scenario.CampaignId}/sides")
        )!;

    [Fact]
    public async Task CreateCampaign_MakesTwoSides()
    {
        using var umpire = await CreateUserClientAsync("umpire@example.com");
        using var created = await umpire.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new CreateCampaignRequest("Leipzig 1813", null),
            CancellationToken
        );
        var campaign = await created.Content.ReadAsAsync<CampaignResponse>();

        var sides =
            await umpire.GetAsAsync<List<SideResponse>>($"/api/campaigns/{campaign!.Id}/sides")
            ?? [];

        Assert.Equal(["Side 1", "Side 2"], sides.Select(s => s.Name).ToList());
        Assert.All(sides, s => Assert.Equal(0, s.ArmyCount));
    }

    [Fact]
    public async Task ListSides_ForEveryMember_CountsTheirArmies()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var sides = await SidesAsync(scenario, Role.Player);

        Assert.Equal(
            [
                new SideResponse(scenario.SideId, "Coalition", 1),
                new SideResponse(scenario.OtherSideId, "French Empire", 0),
            ],
            sides
        );
    }

    [Fact]
    public async Task RenameSide_ByTheUmpire_RenamesIt()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, scenario.SideId, " Sixth Coalition ");

        Assert.Equal("Sixth Coalition", (await response.Content.ReadAsAsync<SideResponse>())?.Name);
        var army = await scenario
            .As(Role.Player)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");
        Assert.Equal("Sixth Coalition", army!.Side.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameSide_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, scenario.SideId, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task RenameSide_ToTheOtherSidesName_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, scenario.OtherSideId, "coalition");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameSide_ItsOwnNameInAnotherCase_IsAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, scenario.SideId, "COALITION");

        Assert.Equal("COALITION", (await response.Content.ReadAsAsync<SideResponse>())?.Name);
    }

    [Theory]
    [InlineData("post")]
    [InlineData("delete")]
    public async Task AddingOrDeletingASide_IsNotARoute(string method)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var umpire = scenario.As(Role.Umpire);

        using var response = string.Equals(method, "post", StringComparison.Ordinal)
            ? await umpire.PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/sides", UriKind.Relative),
                new { name = "Third Coalition" },
                CancellationToken
            )
            : await umpire.DeleteAsync(
                new Uri($"/api/sides/{scenario.SideId}", UriKind.Relative),
                CancellationToken
            );

        Assert.True(
            response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotFound
        );
        Assert.Equal(2, (await SidesAsync(scenario, Role.Umpire)).Count);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListSides_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/sides", UriKind.Relative),
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
    public async Task RenameSide_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, scenario.SideId, "The Allies", role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task RenameSide_Unknown_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, Guid.CreateVersion7(), "Sixth Coalition");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreateArmy_WithoutASide_Returns400()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new { name = "Reserve" },
                CancellationToken
            );

        await response.AssertProblemAsync(HttpStatusCode.BadRequest);
    }
}
