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

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>The first email sent to <paramref name="address"/>, waiting up to 5 seconds.</summary>
    public async Task<EmailMessage> WaitForEmailToAsync(string address)
    {
        for (
            var elapsed = TimeSpan.Zero;
            elapsed < TimeSpan.FromSeconds(5);
            elapsed += TimeSpan.FromMilliseconds(20)
        )
        {
            var email = _sent.FirstOrDefault(e =>
                string.Equals(e.ToAddress, address, StringComparison.OrdinalIgnoreCase)
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
