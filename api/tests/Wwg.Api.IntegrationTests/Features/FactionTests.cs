using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Factions;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Factions (DESIGN.md §5.1), and their §5.2 rows: every member sees them, the Umpire manages them.</summary>
public sealed class FactionTests : ApiTest
{
    private static async Task<FactionResponse> CreateAsync(
        CampaignScenario scenario,
        string name,
        Role role = Role.Umpire
    )
    {
        using var response = await PostCreateAsync(scenario, name, role);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsAsync<FactionResponse>()
            ?? throw new InvalidOperationException("No faction.");
    }

    private static Task<HttpResponseMessage> PostCreateAsync(
        CampaignScenario scenario,
        string name,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/factions", UriKind.Relative),
                new CreateFactionRequest(name),
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> RenameAsync(
        CampaignScenario scenario,
        Guid factionId,
        string name,
        Role role = Role.Umpire
    ) =>
        scenario
            .As(role)
            .PutAsJsonAsync(
                new Uri($"/api/factions/{factionId}", UriKind.Relative),
                new RenameFactionRequest(name),
                TestContext.Current.CancellationToken
            );

    [Fact]
    public async Task CreateFaction_ByTheUmpire_ListsItForEveryMember()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var created = await PostCreateAsync(scenario, " Sixth Coalition ");
        await CreateAsync(scenario, "Allies");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var factions = await scenario
            .As(Role.Player)
            .GetAsAsync<List<FactionResponse>>($"/api/campaigns/{scenario.CampaignId}/factions");
        Assert.Equal(
            "Allies, Coalition, Sixth Coalition",
            string.Join(", ", factions!.Select(f => f.Name)),
            StringComparer.Ordinal
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateFaction_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await PostCreateAsync(scenario, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task CreateFaction_NameTakenInAnyCase_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await CreateAsync(scenario, "Sixth Coalition");

        using var response = await PostCreateAsync(scenario, "SIXTH COALITION");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameFaction_ToAnotherFactionsName_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await CreateAsync(scenario, "Sixth Coalition");
        var france = await CreateAsync(scenario, "France");

        using var response = await RenameAsync(scenario, france.Id, "sixth coalition");

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RenameFaction_ItsOwnNameInAnotherCase_IsAllowed()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var france = await CreateAsync(scenario, "france");

        using var response = await RenameAsync(scenario, france.Id, "France");

        Assert.Equal("France", (await response.Content.ReadAsAsync<FactionResponse>())?.Name);
    }

    [Fact]
    public async Task DeleteFaction_WithArmies_LeavesThemUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // The scenario's army is in its faction.
        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/factions/{scenario.FactionId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var factionId = await WithDbAsync(db =>
            db.Armies.Where(a => a.Id == scenario.ArmyId)
                .Select(a => a.FactionId)
                .SingleAsync(CancellationToken)
        );
        Assert.Null(factionId);
    }

    [Theory]
    [InlineData(Role.Admin, HttpStatusCode.OK)]
    [InlineData(Role.Umpire, HttpStatusCode.OK)]
    [InlineData(Role.Commander, HttpStatusCode.OK)]
    [InlineData(Role.Player, HttpStatusCode.OK)]
    [InlineData(Role.NonMember, HttpStatusCode.NotFound)]
    public async Task ListFactions_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(role)
            .GetAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/factions", UriKind.Relative),
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
    public async Task CreateFaction_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
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
    public async Task RenameFaction_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
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
    public async Task DeleteFaction_ByRole_ReturnsExpectedStatus(Role role, HttpStatusCode expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var coalition = await CreateAsync(scenario, "Sixth Coalition");

        using var response = await scenario
            .As(role)
            .DeleteAsync(
                new Uri($"/api/factions/{coalition.Id}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(expected, response.StatusCode);
    }

    [Fact]
    public async Task RenameFaction_Unknown_Returns404()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await RenameAsync(scenario, Guid.CreateVersion7(), "Sixth Coalition");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }
}
