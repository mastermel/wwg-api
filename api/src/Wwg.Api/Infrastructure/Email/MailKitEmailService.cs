using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Wwg.Api.Infrastructure.Email;

/// <summary>
/// Sends through SMTP with MailKit (System.Net.Mail.SmtpClient is discouraged, can't do implicit
/// TLS on 465 and has no OAuth2). One connection per email: volumes are tiny.
/// </summary>
internal sealed class MailKitEmailService(IOptions<SmtpOptions> options) : IEmailService
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var smtp = options.Value;
        // Only used when Host is set, and then validation requires FromAddress.
        var host = smtp.Host ?? throw new InvalidOperationException("Smtp:Host isn't set.");
        var from =
            smtp.FromAddress ?? throw new InvalidOperationException("Smtp:FromAddress isn't set.");

        using var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(smtp.FromName, from));
        mime.To.Add(new MailboxAddress(message.ToName, message.ToAddress));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(
            host,
            smtp.Port,
            smtp.Security switch
            {
                SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
                SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
                SmtpSecurity.None => SecureSocketOptions.None,
                _ => SecureSocketOptions.Auto,
            },
            cancellationToken
        );
        if (!string.IsNullOrEmpty(smtp.Username))
        {
            await client.AuthenticateAsync(smtp.Username, smtp.Password ?? "", cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
    }
}
