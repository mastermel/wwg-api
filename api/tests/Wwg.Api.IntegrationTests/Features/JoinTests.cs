using System.Net;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Join;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

public sealed class JoinTests : ApiTest
{
    private static async Task<string> JoinCodeAsync(CampaignScenario scenario)
    {
        var response = await scenario
            .As(Role.Umpire)
            .GetAsAsync<JoinCodeResponse>($"/api/campaigns/{scenario.CampaignId}/join-code");
        return response?.JoinCode ?? throw new InvalidOperationException("No join code.");
    }

    private static Task<HttpResponseMessage> JoinAsync(HttpClient client, string code) =>
        client.PostAsync(
            new Uri($"/api/join/{code}", UriKind.Relative),
            null,
            TestContext.Current.CancellationToken
        );

    [Fact]
    public async Task GetJoinPreview_Anonymous_ShowsTheCampaignAndUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync("The Peninsular War");

        var preview = await Client.GetAsAsync<JoinPreviewResponse>(
            $"/api/join/{await JoinCodeAsync(scenario)}"
        );

        Assert.Equal(new JoinPreviewResponse("The Peninsular War", "Test User"), preview);
    }

    [Fact]
    public async Task GetJoinPreview_UnknownCode_Returns404()
    {
        using var response = await Client.GetAsync(
            new Uri("/api/join/not-a-real-code", UriKind.Relative),
            CancellationToken
        );

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinCampaign_NewUser_BecomesAPlayer()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await JoinAsync(
            scenario.As(Role.NonMember),
            await JoinCodeAsync(scenario)
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"/api/campaigns/{scenario.CampaignId}",
            response.Headers.Location?.ToString()
        );
        Assert.Equal(
            new JoinCampaignResponse(scenario.CampaignId, CampaignRole.Player),
            await response.Content.ReadAsAsync<JoinCampaignResponse>()
        );
        var details = await scenario
            .As(Role.NonMember)
            .GetAsAsync<CampaignResponse>($"/api/campaigns/{scenario.CampaignId}");
        Assert.Equal((CampaignRole.Player, 3), (details?.MyRole, details?.PlayerCount));
    }

    [Theory]
    [InlineData(Role.Player, CampaignRole.Player)]
    [InlineData(Role.Umpire, CampaignRole.Umpire)]
    public async Task JoinCampaign_AlreadyAMember_ChangesNothing(Role role, CampaignRole expected)
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await JoinAsync(scenario.As(role), await JoinCodeAsync(scenario));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            new JoinCampaignResponse(scenario.CampaignId, expected),
            await response.Content.ReadAsAsync<JoinCampaignResponse>()
        );
        Assert.Equal(
            3,
            await WithDbAsync(db =>
                Task.FromResult(db.CampaignMembers.Count(m => m.CampaignId == scenario.CampaignId))
            )
        );
    }

    [Fact]
    public async Task JoinCampaign_UnknownCode_Returns404()
    {
        using var client = await CreateUserClientAsync();

        using var response = await JoinAsync(client, "not-a-real-code");

        await response.AssertProblemAsync(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task JoinCampaign_Anonymous_Returns401()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var response = await JoinAsync(Client, await JoinCodeAsync(scenario));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
