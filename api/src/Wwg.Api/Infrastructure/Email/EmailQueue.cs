using System.Threading.Channels;
using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure.Email;

/// <summary>
/// Emails are queued and sent in the background, so a request never waits on SMTP. That also
/// keeps forgot-password's response time the same whether or not the account exists (sending
/// would otherwise add seconds only for real accounts, revealing which emails are registered).
/// </summary>
internal interface IEmailQueue
{
    ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <remarks>
/// A failed send is tried again after <see cref="RetryDelays"/> (then given up and logged), without
/// holding up the emails behind it. Emails still queued or waiting to retry are lost if the app
/// stops; they're a reset link or a notice, which the user can ask for again.
/// </remarks>
internal sealed partial class EmailQueue(
    IEmailService emailService,
    IOptions<SmtpOptions> options,
    TimeProvider time,
    ILogger<EmailQueue> logger
) : BackgroundService, IEmailQueue
{
    /// <summary>How long to wait before each retry: a blip, then an SMTP service that's down.</summary>
    internal static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
    ];

    private readonly Channel<QueuedEmail> _channel = Channel.CreateBounded<QueuedEmail>(
        new BoundedChannelOptions(100) { SingleReader = true }
    );

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(new QueuedEmail(message, Attempt: 1), cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            LogNotConfigured(logger);
        }

        await foreach (var email in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            var message = email.Message;
            try
            {
                await emailService.SendAsync(message, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                if (email.Attempt > RetryDelays.Length)
                {
                    LogGaveUp(logger, exception, message.ToAddress, message.Subject, email.Attempt);
                    continue;
                }

                var delay = RetryDelays[email.Attempt - 1];
                LogWillRetry(logger, exception, message.ToAddress, message.Subject, delay);
                // Not awaited: the next emails go out while this one waits.
                _ = RetryAsync(email with { Attempt = email.Attempt + 1 }, delay, stoppingToken);
            }
        }
    }

    private async Task RetryAsync(
        QueuedEmail email,
        TimeSpan delay,
        CancellationToken stoppingToken
    )
    {
        try
        {
            await Task.Delay(delay, time, stoppingToken);
            await _channel.Writer.WriteAsync(email, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // The app is stopping.
        }
    }

    private sealed record QueuedEmail(EmailMessage Message, int Attempt);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No SMTP host configured (Smtp:Host): emails will be written to the log, not sent"
    )]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Couldn't send email to {To} ({Subject}); trying again in {Delay}"
    )]
    private static partial void LogWillRetry(
        ILogger logger,
        Exception exception,
        string to,
        string subject,
        TimeSpan delay
    );

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Couldn't send email to {To} ({Subject}) after {Attempts} attempts; giving up"
    )]
    private static partial void LogGaveUp(
        ILogger logger,
        Exception exception,
        string to,
        string subject,
        int attempts
    );
}
