using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Armies;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>An army given, a player joining, every army submitted (step 52d, decision 0023).</summary>
public sealed class CampaignEmailTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateArmy_WithACommander_EmailsThem()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var created = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Second Corps", scenario.PlayerMemberId, scenario.SideId),
                Token
            );
        created.EnsureSuccessStatusCode();

        var email = await Emails.WaitForEmailToAsync("player@example.com", "you command");
        Assert.Equal("The Peninsular War: you command Second Corps", email.Subject);
        Assert.Contains(
            "You've been given command of Second Corps in The Peninsular War.",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task AssignCommander_EmailsTheNewCommander()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        using var assigned = await scenario
            .As(Role.Umpire)
            .PutAsJsonAsync(
                new Uri($"/api/armies/{scenario.ArmyId}/commander", UriKind.Relative),
                new AssignCommanderRequest(scenario.PlayerMemberId),
                Token
            );
        assigned.EnsureSuccessStatusCode();

        var email = await Emails.WaitForEmailToAsync("player@example.com", "you command");
        Assert.Equal("The Peninsular War: you command First Corps", email.Subject);
    }

    [Fact]
    public async Task JoinCampaign_EmailsTheUmpire()
    {
        using var scenario = await CreateCampaignScenarioAsync();

        var email = await Emails.WaitForEmailToAsync("umpire@example.com", "joined");

        Assert.Equal("The Peninsular War: Test User joined", email.Subject);
        Assert.Contains(
            $"/campaigns/{scenario.CampaignId}",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SubmitTurn_TheLastArmyIn_EmailsTheUmpireWhatsWaiting()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);

        await TurnSteps.SubmittedAsync(scenario);

        var email = await Emails.WaitForEmailToAsync("umpire@example.com", "every army");
        Assert.Equal("The Peninsular War: every army has submitted turn 1", email.Subject);
        Assert.Contains(
            "The army has submitted turn 1 of The Peninsular War: approve them and start turn 2.",
            email.TextBody,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "Nothing else is waiting for you as you start the next turn.",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task SubmitTurn_TheUmpireSubmittingTheLastArmy_EmailsTheUmpireWhatsWaiting()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        var turn = await TurnSteps.OpenArmyTurnAsync(scenario);
        using var held = await TurnSteps.OrderAsync(
            scenario,
            turn.Id,
            TurnSteps.Hold,
            role: Role.Umpire
        );

        using var submitted = await TurnSteps.ActAsync(scenario, turn.Id, "submit", Role.Umpire);

        submitted.EnsureSuccessStatusCode();
        await Emails.WaitForEmailToAsync("umpire@example.com", "every army");
    }

    [Fact]
    public async Task SubmitTurn_WithAnArmyStillADraft_SaysNothingOfEveryArmy()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.ReadyAsync(scenario);
        using var other = await scenario
            .As(Role.Umpire)
            .PostAsJsonAsync(
                new Uri($"/api/campaigns/{scenario.CampaignId}/armies", UriKind.Relative),
                new CreateArmyRequest("Second Corps", null, scenario.OtherSideId),
                Token
            );
        other.EnsureSuccessStatusCode();
        using var started = await TurnSteps.StartAsync(scenario);
        started.EnsureSuccessStatusCode();

        await TurnSteps.SubmittedAsync(scenario);

        // The submitted turn's own email, queued after any every-army one would be.
        await Emails.WaitForEmailToAsync("umpire@example.com", "submitted turn 1");
        Assert.DoesNotContain(
            Emails.Sent,
            e => e.Subject.Contains("every army", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task JoinCampaign_TurnedOff_EmailsTheUmpireNothing()
    {
        using var umpire = await CreateUserClientAsync("umpire@example.com");
        using var muted = await umpire.PutAsJsonAsync(
            new Uri("/api/me/email-settings", UriKind.Relative),
            new UpdateEmailSettingsRequest([EmailKind.PlayerJoined]),
            Token
        );
        muted.EnsureSuccessStatusCode();
        using var created = await umpire.PostAsJsonAsync(
            new Uri("/api/campaigns", UriKind.Relative),
            new Wwg.Api.Features.Campaigns.CreateCampaignRequest("Quiet", null),
            Token
        );
        var campaign =
            await created.Content.ReadAsAsync<Wwg.Api.Features.Campaigns.CampaignResponse>();
        var code = await umpire.GetAsAsync<Wwg.Api.Features.Campaigns.JoinCodeResponse>(
            $"/api/campaigns/{campaign!.Id}/join-code"
        );
        using var joiner = await CreateUserClientAsync("joiner@example.com");

        using var joined = await joiner.PostAsync(
            new Uri($"/api/join/{code!.JoinCode}", UriKind.Relative),
            null,
            Token
        );
        joined.EnsureSuccessStatusCode();

        // Queued after the join's would be: the joiner's password reset.
        using var reset = await Client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            new Wwg.Api.Features.Auth.ForgotPasswordRequest("joiner@example.com"),
            Token
        );
        await Emails.WaitForEmailToAsync("joiner@example.com", "Reset");
        Assert.DoesNotContain(
            Emails.Sent,
            e => e.Subject.Contains("joined", StringComparison.Ordinal)
        );
    }
}
