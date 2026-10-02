using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>The new turn's email (step 52c, decision 0023); each kind of news is tested with its feature.</summary>
public sealed class TurnStartedEmailTests : ApiTest
{
    [Fact]
    public async Task StartCampaign_EmailsEachCommander_WithTheTurnsDayAndTimeOfDay()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.SetCalendarAsync(scenario, TurnPart.Afternoon, new DateOnly(1815, 6, 16));

        await TurnSteps.StartedAsync(scenario);

        var email = await Emails.WaitForEmailToAsync("commander@example.com");
        Assert.Equal(
            "The Peninsular War: turn 1 has started (16 June 1815, Afternoon)",
            email.Subject
        );
        Assert.Contains(
            "The Peninsular War has begun: turn 1 (16 June 1815, Afternoon) is open. Give First Corps its first orders.",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StartNextTurn_WithNothingNew_SaysSo()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);

        await TurnSteps.PlayAsync(scenario, "hold");

        var email = await Emails.WaitForEmailToAsync("commander@example.com", "turn 2 has started");
        Assert.Equal("The Peninsular War: turn 2 has started (Afternoon)", email.Subject);
        Assert.Contains(
            "Nothing new since the last turn.",
            email.TextBody,
            StringComparison.Ordinal
        );
        Assert.Contains(
            $"/campaigns/{scenario.CampaignId}/map",
            email.TextBody,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task StartNextTurn_TurnedOff_EmailsTheCommanderNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        using var muted = await scenario
            .As(Role.Commander)
            .PutAsJsonAsync(
                new Uri("/api/me/email-settings", UriKind.Relative),
                new Wwg.Api.Features.Account.UpdateEmailSettingsRequest([EmailKind.TurnStarted]),
                TestContext.Current.CancellationToken
            );
        muted.EnsureSuccessStatusCode();

        await TurnSteps.StartedAsync(scenario);
        await TurnSteps.PlayAsync(scenario, "hold");

        // The Umpire's own, of the submitting, went after the first turn's would have.
        await Emails.WaitForEmailToAsync("umpire@example.com");
        Assert.DoesNotContain(
            Emails.Sent,
            e => e.Subject.Contains("has started", StringComparison.Ordinal)
        );
    }
}
