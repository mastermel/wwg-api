namespace Wwg.Api.Infrastructure.Email;

/// <summary>
/// Used when no SMTP host is configured: writes each email to the log instead of sending it.
/// Note that reset links then appear in the log, so configure SMTP before real use.
/// </summary>
internal sealed partial class LoggingEmailService(ILogger<LoggingEmailService> logger)
    : IEmailService
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        LogEmail(logger, message.ToAddress, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Email not sent (no SMTP host configured). To: {To}. Subject: {Subject}.\n{Body}"
    )]
    private static partial void LogEmail(ILogger logger, string to, string subject, string body);
}
