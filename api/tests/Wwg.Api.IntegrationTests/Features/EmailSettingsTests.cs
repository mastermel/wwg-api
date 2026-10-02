using System.Net;
using System.Net.Http.Json;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Account;
using Wwg.Api.Features.Turns;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests.Features;

/// <summary>Turning campaign emails off (step 52a, decision 0023).</summary>
public sealed class EmailSettingsTests : ApiTest
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Task<HttpResponseMessage> MuteAsync(
        HttpClient client,
        params EmailKind[] kinds
    ) =>
        client.PutAsJsonAsync(
            new Uri("/api/me/email-settings", UriKind.Relative),
            new UpdateEmailSettingsRequest(kinds),
            Token
        );

    [Fact]
    public async Task GetEmailSettings_ANewAccount_GetsEveryEmail()
    {
        using var client = await CreateUserClientAsync();

        var settings = await client.GetAsAsync<EmailSettingsResponse>("/api/me/email-settings");

        Assert.Empty(settings!.Muted);
    }

    [Fact]
    public async Task UpdateEmailSettings_SomeTurnedOff_AreKept()
    {
        using var client = await CreateUserClientAsync();

        using var muted = await MuteAsync(client, EmailKind.PlayerJoined, EmailKind.ArmySubmitted);

        muted.EnsureSuccessStatusCode();
        var settings = await client.GetAsAsync<EmailSettingsResponse>("/api/me/email-settings");
        Assert.Equal([EmailKind.ArmySubmitted, EmailKind.PlayerJoined], settings!.Muted);
    }

    [Fact]
    public async Task UpdateEmailSettings_SignedOut_Returns401()
    {
        using var muted = await MuteAsync(Client, EmailKind.PlayerJoined);

        Assert.Equal(HttpStatusCode.Unauthorized, muted.StatusCode);
    }

    [Fact]
    public async Task SubmitTurn_WithTheUmpiresTurnedOff_EmailsThemNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        using var muted = await MuteAsync(scenario.As(Role.Umpire), EmailKind.ArmySubmitted);
        muted.EnsureSuccessStatusCode();

        var turn = await TurnSteps.SubmittedAsync(scenario);
        // Sent after it, in order: once the commander has theirs, the Umpire's would have gone.
        using var sentBack = await TurnSteps.ActAsync(
            scenario,
            turn.Id,
            "send-back",
            Role.Umpire,
            new ReviewTurnRequest("Again, please.", [])
        );
        sentBack.EnsureSuccessStatusCode();
        await Emails.WaitForEmailToAsync("commander@example.com", "sent back for");

        Assert.DoesNotContain(
            Emails.Sent,
            e =>
                string.Equals(e.ToAddress, "umpire@example.com", StringComparison.OrdinalIgnoreCase)
                && e.Subject.Contains("First Corps submitted turn", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task ApproveTurn_WithTheCommandersTurnedOff_EmailsThemNothing()
    {
        using var scenario = await CreateCampaignScenarioAsync();
        await TurnSteps.StartedAsync(scenario);
        using var muted = await MuteAsync(scenario.As(Role.Commander), EmailKind.TurnReviewed);
        muted.EnsureSuccessStatusCode();
        var turn = await TurnSteps.SubmittedAsync(scenario);

        using var approved = await TurnSteps.ActAsync(scenario, turn.Id, "approve", Role.Umpire);
        approved.EnsureSuccessStatusCode();
        using var next = await TurnSteps.StartNextTurnAsync(scenario, request: new([], []));
        next.EnsureSuccessStatusCode();

        // The new turn's email, sent after the approval's would have been.
        await Emails.WaitForEmailToAsync("commander@example.com", "turn 2 has started");
        Assert.DoesNotContain(
            Emails.Sent,
            e => e.Subject.Contains("approved for", StringComparison.Ordinal)
        );
    }
}
