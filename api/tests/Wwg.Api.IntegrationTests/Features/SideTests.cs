using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Sides;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Sides (DESIGN.md §5.1), and their §5.2 rows: every member sees them, the Umpire manages them.</summary>
public sealed class SideTests : ApiTest
{
    private static async Task<SideResponse> CreateAsync(
        CampaignScenario scenario,
        string name,
        Role role = Role.Umpire
    )
    {
        using var response = await PostCreateAsync(scenario, name, role);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsAsync<SideResponse>()
            ?? throw new InvalidOperationException("No side.");
    }

    private static Task<HttpResponseMessage> PostCreateAsync(
        CampaignScenario scenario,
        string name,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/sides", UriKind.Relative),
                new CreateSideRequest(name),
                TestContext.Current.CancellationToken
            );

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

    [Fact]
    public async Task CreateSide_ByTheUmpire_ListsItForEveryMember()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var created = await PostCreateAsync(scenario, " Sixth Coalition ");
        await CreateAsync(scenario, "Allies");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var sides = await scenario
            .As(Role.Player)
            .GetAsAsync<List<SideResponse>>($"/api/campaigns/{scenario.CampaignId}/sides");
        Assert.Equal(
            "Allies, Coalition, Sixth Coalition",
            string.Join(", ", sides!.Select(f => f.Name)),
            StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateSide_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PostCreateAsync(scenario, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task CreateSide_NameTakenInAnyCase_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await CreateAsync(scenario, "Sixth Coalition");

        using var response = await PostCreateAsync(scenario, "SIXTH COALITION");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameSide_ToAnotherSidesName_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await CreateAsync(scenario, "Sixth Coalition");
        var france = await CreateAsync(scenario, "France");

        using var response = await RenameAsync(scenario, france.Id, "sixth coalition");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameSide_ItsOwnNameInAnotherCase_IsAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var france = await CreateAsync(scenario, "france");

        using var response = await RenameAsync(scenario, france.Id, "France");

        Assert.Equal("France", (await response.Content.ReadAsAsync<SideResponse>())?.Name);
    }

    [Fact]
    public async Task DeleteSide_WithArmies_LeavesThemUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // The scenario's army is in its side.
        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/sides/{scenario.SideId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var sideId = await WithDbAsync(db =>
            db.Armies.Where(a => a.Id == scenario.ArmyId)
                .Select(a => a.SideId)
                .SingleAsync(CancellationToken)
        );
        Assert.Null(sideId);
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
    [InlineData(Role.Admin, HttpStatusCode.Created)]
    [InlineData(Role.Umpire, HttpStatusCode.Created)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task CreateSide_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PostCreateAsync(scenario, "Sixth Coalition", role);

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
        var coalition = await CreateAsync(scenario, "Sixth Coalition");

        using var response = await RenameAsync(scenario, coalition.Id, "The Allies", role);

        Assert.Equal(expected, response.StatusCode);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.NoContent)]
    [InlineData(Role.Umpire, HttpStatusCode.NoContent)]
    [InlineData(Role.Commander, HttpStatusCode.Forbidden)]
    [InlineData(Role.Player, HttpStatusCode.Forbidden)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task DeleteSide_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var coalition = await CreateAsync(scenario, "Sixth Coalition");

        using var response = await scenario
            .As(role)
            .DeleteAsync(
                new Uri($"/api/sides/{coalition.Id}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task RenameSide_Unknown_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, Guid.CreateVersion7(), "Sixth Coalition");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
