using System.Collections.Concurrent;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// Records emails instead of sending them. The real queue still delivers them (in the
/// background), so tests wait for an email rather than expecting it immediately.
/// </summary>
internal sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private int _failuresLeft;
    private int _attempts;

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    /// <summary>How many times sending was tried, failures included.</summary>
    public int Attempts => Volatile.Read(ref _attempts);

    /// <summary>The next <paramref name="count"/> sends fail, as if SMTP were down.</summary>
    public void FailNext(int count) => Volatile.Write(ref _failuresLeft, count);

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _attempts);
        if (Interlocked.Decrement(ref _failuresLeft) >= 0)
        {
            throw new InvalidOperationException("Simulated SMTP failure.");
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>
    /// The first email sent to <paramref name="address"/>, waiting up to 5 seconds, leaving out the
    /// welcome every new account gets (decision 0023): tests make accounts as they go.
    /// </summary>
    public Task<EmailMessage> WaitForEmailToAsync(string address) =>
        WaitForAsync(address, e => !e.Subject.StartsWith("Welcome to", StringComparison.Ordinal));

    /// <summary>The first email to <paramref name="address"/> whose subject has this in it.</summary>
    public Task<EmailMessage> WaitForEmailToAsync(string address, string subject) =>
        WaitForAsync(address, e => e.Subject.Contains(subject, StringComparison.Ordinal));

    /// <summary>The welcome sent to <paramref name="address"/> on registering.</summary>
    public Task<EmailMessage> WaitForWelcomeToAsync(string address) =>
        WaitForAsync(address, e => e.Subject.StartsWith("Welcome to", StringComparison.Ordinal));

    private async Task<EmailMessage> WaitForAsync(string address, Func<EmailMessage, bool> which)
    {
        for (
            var elapsed = TimeSpan.Zero;
            elapsed < TimeSpan.FromSeconds(5);
            elapsed += TimeSpan.FromMilliseconds(20)
        )
        {
            var email = _sent.FirstOrDefault(e =>
                string.Equals(e.ToAddress, address, StringComparison.OrdinalIgnoreCase) && which(e)
            );
            if (email is not null)
            {
                return email;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No email was sent to {address}.");
    }
}
