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

internal sealed partial class EmailQueue(
    IEmailService emailService,
    IOptions<SmtpOptions> options,
    ILogger<EmailQueue> logger
) : BackgroundService, IEmailQueue
{
    private readonly Channel<EmailMessage> _channel = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(100) { SingleReader = true }
    );

    public ValueTask QueueAsync(EmailMessage message, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(message, cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            LogNotConfigured(logger);
        }

        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await emailService.SendAsync(message, stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // One failed email mustn't stop the rest; there's no retry yet.
                LogSendFailed(logger, exception, message.ToAddress, message.Subject);
            }
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "No SMTP host configured (Smtp:Host): emails will be written to the log, not sent"
    )]
    private static partial void LogNotConfigured(ILogger logger);

    [LoggerMessage(Level = LogLevel.Error, Message = "Couldn't send email to {To} ({Subject})")]
    private static partial void LogSendFailed(
        ILogger logger,
        Exception exception,
        string to,
        string subject
    );
}
