using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class ArmyTests : ApiTest
{
    private static Task<HttpResponseMessage> CreateAsync(
        CampaignScenario scenario,
        string name,
        Guid? commanderMemberId = null
    ) =>
        scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest(name, commanderMemberId),
                TestContext.Current.CancellationToken
            );

    private static Task<HttpResponseMessage> AssignAsync(
        CampaignScenario scenario,
        Guid armyId,
        Guid memberId
    ) =>
        scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{armyId}/commander", UriKind.Relative),
                new AssignCommanderRequest(memberId),
                TestContext.Current.CancellationToken
            );

    private static async Task<ArmySummary> FirstCorpsAsync(CampaignScenario scenario)
    {
        var armies = await scenario
            .As(Role.Player)
            .GetAsAsync<List<ArmySummary>>($"/api/campaigns/{scenario.CampaignId}/armies");
        return armies!.Single(a => a.Id == scenario.ArmyId);
    }

    [Fact]
    public async Task CreateArmy_WithoutACommander_IsUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        using var response = await CreateAsync(scenario, "  Second Corps ");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var army = await response.Content.ReadAsAsync<ArmyResponse>();
        Assert.Equal($"/api/armies/{army?.Id}", response.Headers.Location?.ToString());
        Assert.Equal(
            ("Second Corps", "The Peninsular War", null),
            (army?.Name, army?.CampaignName, army?.Commander)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateArmy_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task CreateArmy_CommandedByTheUmpire_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(scenario, "Second Corps", scenario.UmpireMemberId);

        await response.AssertValidationProblemAsync("commanderMemberId");
    }

    [Fact]
    public async Task CreateArmy_CommandedByAnUnknownMember_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var response = await CreateAsync(scenario, "Second Corps", Guid.CreateVersion7());

        await response.AssertValidationProblemAsync("commanderMemberId");
    }

    [Fact]
    public async Task CreateArmy_CommandedBySomeoneWhoAlreadyCommands_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await CreateAsync(
            scenario,
            "Second Corps",
            scenario.CommanderMemberId
        );

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task ListArmies_SortsByNameIgnoringCaseAndIncludesUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        (await CreateAsync(scenario, "reserve")).Dispose();
        (await CreateAsync(scenario, "Artillery Park", scenario.PlayerMemberId)).Dispose();

        var armies = await scenario
            .As(Role.Player)
            .GetAsAsync<List<ArmySummary>>($"/api/campaigns/{scenario.CampaignId}/armies");

        Assert.Equal(
            [
                ("Artillery Park", scenario.PlayerMemberId),
                ("First Corps", scenario.CommanderMemberId),
                ("reserve", (Guid?)null),
            ],
            armies?.Select(a => (a.Name, a.Commander?.MemberId))
        );
    }

    [Fact]
    public async Task GetArmy_ByItsCommander_ShowsItAndItsCampaign()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        var army = await scenario
            .As(Role.Commander)
            .GetAsAsync<ArmyResponse>($"/api/armies/{scenario.ArmyId}");

        Assert.Equal(
            ("First Corps", scenario.CampaignId, "The Peninsular War", scenario.CommanderMemberId),
            (army?.Name, army?.CampaignId, army?.CampaignName, army?.Commander?.MemberId)
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RenameArmy_BlankName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new RenameArmyRequest(name),
                CancellationToken
            );

        await response.AssertValidationProblemAsync("name");
    }

    [Fact]
    public async Task AssignCommander_NoMember_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await AssignAsync(scenario, scenario.ArmyId, Guid.Empty);

        await response.AssertValidationProblemAsync("memberId");
    }

    [Fact]
    public async Task RenameArmy_ChangesItsName()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                new RenameArmyRequest(" Imperial Guard "),
                CancellationToken
            );

        Assert.Equal("Imperial Guard", (await response.Content.ReadAsAsync<ArmyResponse>())?.Name);
        Assert.Equal("Imperial Guard", (await FirstCorpsAsync(scenario)).Name);
    }

    [Fact]
    public async Task DeleteArmy_RemovesItAndFreesItsCommander()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var armies = await scenario
            .As(Role.Umpire)
            .GetAsAsync<List<ArmySummary>>($"/api/campaigns/{scenario.CampaignId}/armies");
        Assert.Empty(armies!);
        using var again = await CreateAsync(scenario, "Second Corps", scenario.CommanderMemberId);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
    }

    [Fact]
    public async Task AssignCommander_AnotherPlayer_ReplacesTheCommander()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await AssignAsync(scenario, scenario.ArmyId, scenario.PlayerMemberId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            scenario.PlayerMemberId,
            (await FirstCorpsAsync(scenario)).Commander?.MemberId
        );
        // The old commander no longer sees the army's details.
        using var details = await scenario
            .As(Role.Commander)
            .GetAsync(
                new Uri($"/api/armies/{scenario.ArmyId}", UriKind.Relative),
                CancellationToken
            );
        Assert.Equal(HttpStatusCode.Forbidden, details.StatusCode);
    }

    [Fact]
    public async Task AssignCommander_TheSameCommander_ChangesNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await AssignAsync(
            scenario,
            scenario.ArmyId,
            scenario.CommanderMemberId
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AssignCommander_APlayerWhoCommandsAnotherArmy_Returns409()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var created = await CreateAsync(scenario, "Second Corps");
        var second = await created.Content.ReadAsAsync<ArmyResponse>();

        using var response = await AssignAsync(scenario, second!.Id, scenario.CommanderMemberId);

        await response.AssertProblemAsync(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task AssignCommander_TheUmpire_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await AssignAsync(scenario, scenario.ArmyId, scenario.UmpireMemberId);

        await response.AssertValidationProblemAsync("memberId");
    }

    [Fact]
    public async Task UnassignCommander_LeavesTheArmyWithoutOne()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/commander", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await FirstCorpsAsync(scenario)).Commander);
    }

    [Fact]
    public async Task UnassignCommander_Always_UpdatesTheArmysUpdatedAt()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        // Less than the access token's 30 minutes.
        Clock.Advance(TimeSpan.FromMinutes(1));

        using var response = await scenario
            .As(Role.Umpire)
            .DeleteAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/commander", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var updatedAt = await WithDbAsync(db =>
            db.Armies.Where(a => a.Id == scenario.ArmyId)
                .Select(a => a.UpdatedAt)
                .SingleAsync(CancellationToken)
        );
        Assert.Equal(Clock.GetUtcNow().UtcDateTime, updatedAt);
    }

    [Fact]
    public async Task TheCommanderLeaving_KeepsTheArmyUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Commander)
            .DeleteAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/members/me", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await FirstCorpsAsync(scenario)).Commander);
    }

    [Fact]
    public async Task CreateArmy_CommandedByAMemberOfAnotherCampaign_IsAValidationError()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var other = await scenario
            .As(Role.NonMember)
            .PostAsJsonAsync(
                new Uri("/api/campaigns", UriKind.Relative),
                new CreateCampaignRequest("Another campaign", null),
                CancellationToken
            );
        var otherUmpire = (await other.Content.ReadAsAsync<CampaignResponse>())?.Umpire?.MemberId;

        using var response = await CreateAsync(scenario, "Second Corps", otherUmpire);

        await response.AssertValidationProblemAsync("commanderMemberId");
    }

    [Fact]
    public async Task SetUmpire_TheCommander_LeavesTheirArmyUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var commanderUserId = await WithDbAsync(db =>
            Task.FromResult(
                db.CampaignMembers.Single(m => m.Id == scenario.CommanderMemberId).UserId
            )
        );

        using var response = await scenario
            .As(Role.Admin)
            .PutAsJsonAsync(
                new Uri($"/api/admin/campaigns/{scenario.CampaignId}/umpire", UriKind.Relative),
                new SetUmpireRequest(commanderUserId),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null((await FirstCorpsAsync(scenario)).Commander);
    }

    [Fact]
    public async Task DeleteUser_TheCommander_LeavesTheirArmyUnassigned()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var commanderUserId = await WithDbAsync(db =>
            Task.FromResult(
                db.CampaignMembers.Single(m => m.Id == scenario.CommanderMemberId).UserId
            )
        );

        using var response = await scenario
            .As(Role.Admin)
            .DeleteAsync(
                new Uri($"/api/admin/users/{commanderUserId}", UriKind.Relative),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null((await FirstCorpsAsync(scenario)).Commander);
    }

    [Fact]
    public async Task ListMembers_ShowsTheArmyEachPlayerCommands()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var members = await scenario
            .As(Role.Player)
            .GetAsAsync<List<CampaignMemberResponse>>(
                $"/api/campaigns/{scenario.CampaignId}/members"
            );

        var commander = members!.Single(m => m.Id == scenario.CommanderMemberId);
        Assert.Equal(new MemberArmy(scenario.ArmyId, "First Corps"), commander.Army);
    }
}
