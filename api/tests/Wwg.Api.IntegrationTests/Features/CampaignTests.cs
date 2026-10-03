using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class CampaignTests : ApiTest
{
    private static Task<HttpResponseMessage> CreateAsync(
        HttpClient client,
        string name,
        string? description = null
    ) =>
        client.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new CreateCampaignRequest(name, description),
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task CreateCampaign_Valid_MakesTheCallerItsUmpire()
    {
        using var client = await CreateUserClientAsync("mel@example.com");

        using var response = await CreateAsync(
            client,
            "  The Hundred Days ",
            "  Waterloo and all that. "
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var campaign = await response.Content.ReadAsAsync<CampaignResponse>();
        Assert.Equal($"/api/campaigns/{campaign?.Id}", response.Headers.Location?.ToString());
        Assert.Equal(
            ("The Hundred Days", "Waterloo and all that."),
            (campaign?.Name, campaign?.Description)
        );
        Assert.Equal(CampaignRole.Umpire, campaign?.MyRole);
        Assert.Equal("Test", campaign?.Umpire?.FirstName);
        Assert.Equal(0, campaign?.PlayerCount);
    }

    [Fact]
    public async Task CreateCampaign_Valid_GetsA22CharacterJoinCode()
    {
        using var client = await CreateUserClientAsync();

        using var response = await CreateAsync(client, "Campaign");

        var code = await WithDbAsync(db =>
            db.Campaigns.Select(c => c.JoinCode).SingleAsync(CancellationToken)
        );
        Assert.Matches("^[A-Za-z0-9_-]{22}$", code);
    }

    [Fact]
    public async Task CreateCampaign_BlankDescription_StoresNone()
    {
        using var client = await CreateUserClientAsync();

        using var response = await CreateAsync(client, "Campaign", "   ");

        Assert.Null((await response.Content.ReadAsAsync<CampaignResponse>())?.Description);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(
        "A name that is far too long, going on and on well past the one hundred characters that a campaign name is allowed"
    )]
    public async Task CreateCampaign_BadName_IsAValidationError(string name)
    {
        using var client = await CreateUserClientAsync();

        using var response = await CreateAsync(client, name);

        await response.AssertValidationProblemAsync("name");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData(
        "A name that is far too long, going on and on well past the one hundred characters that a campaign name is allowed"
    )]
    public async Task UpdateCampaign_BadName_IsAValidationError(string name)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                new UpdateCampaignRequest(name, ""),
                CancellationToken
            );

        await response.AssertValidationProblemAsync("name");
    }

    [Theory]
    [InlineData("?pageSize=101", "pageSize")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?page=0", "page")]
    public async Task ListMyCampaigns_PagingOutOfRange_IsAValidationError(
        string query,
        string field
    )
    {
        using var client = await CreateUserClientAsync();

        using var response = await client.GetAsync(
            new Uri($"/api/campaigns{query}", UriKind.Relative),
            CancellationToken
        );

        await response.AssertValidationProblemAsync(field);
    }

    [Fact]
    public async Task CreateCampaign_Anonymous_Returns401()
    {
        using var response = await CreateAsync(Client, "Campaign");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ListMyCampaigns_ShowsEachMemberTheirOwnRoleAndOutsidersNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        var umpire = await scenario
            .As(Role.Umpire)
            .GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");
        var player = await scenario
            .As(Role.Player)
            .GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");
        var outsider = await scenario
            .As(Role.NonMember)
            .GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");

        Assert.Equal(CampaignRole.Umpire, Assert.Single(umpire!.Items).MyRole);
        var seen = Assert.Single(player!.Items);
        Assert.Equal(
            (CampaignRole.Player, "Test User", 2),
            (seen.MyRole, seen.UmpireName, seen.PlayerCount)
        );
        Assert.Empty(outsider!.Items);
    }

    [Fact]
    public async Task ListMyCampaigns_FlagsACampaignOnceItHasStarted()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var umpire = scenario.As(Role.Umpire);

        var before = await umpire.GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");
        await TurnSteps.StartedAsync(scenario);
        var after = await umpire.GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");

        Assert.False(Assert.Single(before!.Items).Started);
        Assert.True(Assert.Single(after!.Items).Started);
    }

    [Fact]
    public async Task ListMyCampaigns_SortsByNameIgnoringCase()
    {
        using var client = await CreateUserClientAsync();
        foreach (var name in new[] { "waterloo", "Austerlitz", "borodino" })
        {
            (await CreateAsync(client, name)).EnsureSuccessStatusCode();
        }

        var list = await client.GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");

        Assert.Equal(
            ["Austerlitz", "borodino", "waterloo"],
            list!.Items.Select(c => c.Name),
            StringComparer.Ordinal
        );
    }

    [Fact]
    public async Task GetCampaign_AdminWhoIsNotAMember_SeesItWithNoRole()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var campaign = await scenario
            .As(Role.Admin)
            .GetAsAsync<CampaignResponse>($"/api/campaigns/{scenario.CampaignId}");

        Assert.Null(campaign?.MyRole);
        Assert.Equal(2, campaign?.PlayerCount);
        Assert.NotNull(campaign?.Umpire);
    }

    [Fact]
    public async Task UpdateCampaign_ByTheUmpire_ChangesItAndItsUpdatedTime()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        var before = await scenario
            .As(Role.Umpire)
            .GetAsAsync<CampaignResponse>($"/api/campaigns/{scenario.CampaignId}");

        // Less than the access token's 30 minutes.
        Clock.Advance(TimeSpan.FromMinutes(10));
        using var response = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                new UpdateCampaignRequest("Renamed", ""),
                CancellationToken
            );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var after = await response.Content.ReadAsAsync<CampaignResponse>();
        Assert.Equal(("Renamed", (string?)null), (after?.Name, after?.Description));
        Assert.Equal(before!.UpdatedAt + TimeSpan.FromMinutes(10), after?.UpdatedAt);
        Assert.Equal(CampaignRole.Umpire, after?.MyRole);
    }

    [Fact]
    public async Task DeleteCampaign_ByTheUmpire_RemovesItForEveryone()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        (
            await scenario
                .As(Role.Umpire)
                .DeleteAsync(
                    new Uri($"/api/campaigns/{scenario.CampaignId}", UriKind.Relative),
                    CancellationToken
                )
        ).EnsureSuccessStatusCode();
        var playerList = await scenario
            .As(Role.Player)
            .GetAsAsync<PagedResponse<CampaignSummary>>("/api/campaigns");

        Assert.Empty(playerList!.Items);
        Assert.Equal(0, await WithDbAsync(db => db.CampaignMembers.CountAsync(CancellationToken)));
    }
}
