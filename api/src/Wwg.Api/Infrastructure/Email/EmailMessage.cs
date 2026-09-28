namespace Wwg.Api.Infrastructure.Email;

/// <summary>An email to send, with an HTML body and a plain-text alternative.</summary>
internal sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,
    string HtmlBody,
    string TextBody
);

/// <summary>
/// Sends one email. Our own abstraction, deliberately not Identity's <c>IEmailSender</c>, which
/// doesn't fit emails like the email-changed notice.
/// </summary>
internal interface IEmailService
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
