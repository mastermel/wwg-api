using System.Net.Http.Json;
using Wwg.Api.Features.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

public sealed class EmailQueueTests : ApiTest
{
    [Fact]
    public async Task Send_FailsOnce_TriesAgainAndSends()
    {
        await RegisterAsync(Client, "mel@example.com");
        Emails.FailNext(1);

        await ForgotPasswordAsync("mel@example.com");

        await WaitUntilAsync(() => Emails.Sent.Count == 1, advanceClock: true);
        Assert.Equal(2, Emails.Attempts);
    }

    [Fact]
    public async Task Send_Fails_DoesNotHoldUpTheNextEmail()
    {
        await RegisterAsync(Client, "mel@example.com");
        await RegisterAsync(Client, "ada@example.com");
        Emails.FailNext(1);

        await ForgotPasswordAsync("mel@example.com");
        await ForgotPasswordAsync("ada@example.com");

        // Without moving the clock: the first email is still waiting to be tried again.
        var sent = await Emails.WaitForEmailToAsync("ada@example.com");
        Assert.Equal("ada@example.com", sent.ToAddress, StringComparer.Ordinal);
        Assert.DoesNotContain(
            Emails.Sent,
            e => string.Equals(e.ToAddress, "mel@example.com", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Send_KeepsFailing_GivesUpAfterTheRetries()
    {
        await RegisterAsync(Client, "mel@example.com");
        Emails.FailNext(100);

        await ForgotPasswordAsync("mel@example.com");

        var attempts = 1 + EmailQueueRetries;
        await WaitUntilAsync(() => Emails.Attempts == attempts, advanceClock: true);
        Clock.Advance(TimeSpan.FromHours(1));
        await Task.Delay(TimeSpan.FromMilliseconds(100), CancellationToken);
        Assert.Equal(attempts, Emails.Attempts);
    }

    private static int EmailQueueRetries => Api.Infrastructure.Email.EmailQueue.RetryDelays.Length;

    private async Task ForgotPasswordAsync(string email)
    {
        using var response = await Client.PostAsJsonAsync(
            new Uri("/api/auth/forgot-password", UriKind.Relative),
            new ForgotPasswordRequest(email),
            CancellationToken
        );
        response.EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Waits up to 5 seconds for <paramref name="condition"/>, optionally moving the fake clock on
    /// as it goes, so retry delays pass.
    /// </summary>
    private async Task WaitUntilAsync(Func<bool> condition, bool advanceClock)
    {
        for (
            var elapsed = TimeSpan.Zero;
            elapsed < TimeSpan.FromSeconds(5);
            elapsed += TimeSpan.FromMilliseconds(20)
        )
        {
            if (condition())
            {
                return;
            }

            if (advanceClock)
            {
                Clock.Advance(TimeSpan.FromSeconds(10));
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), CancellationToken);
        }

        throw new TimeoutException("The condition wasn't met in time.");
    }
}
