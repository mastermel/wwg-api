using System.ComponentModel.DataAnnotations;

namespace Wwg.Api.Infrastructure.Email;

internal enum SmtpSecurity
{
    /// <summary>STARTTLS on port 587, TLS on connect on 465, none otherwise.</summary>
    Auto,
    StartTls,
    SslOnConnect,
    None,
}

/// <summary>
/// Outgoing email (<c>Smtp</c> section). With no <see cref="Host"/>, emails are written to the
/// log instead of sent, so the app runs before a real SMTP service is set up.
/// </summary>
internal sealed class SmtpOptions : IValidatableObject
{
    public const string SectionName = "Smtp";

    public string? Host { get; set; }

    [Range(1, 65535)]
    public int Port { get; set; } = 587;

    public SmtpSecurity Security { get; set; } = SmtpSecurity.StartTls;

    public string? Username { get; set; }

    public string? Password { get; set; }

    [EmailAddress]
    public string? FromAddress { get; set; }

    public string FromName { get; set; } = "Wasatch Wargamers";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsConfigured && string.IsNullOrWhiteSpace(FromAddress))
        {
            yield return new ValidationResult(
                "FromAddress is required when Host is set.",
                [nameof(FromAddress)]
            );
        }

        if (!string.IsNullOrEmpty(Username) && string.IsNullOrEmpty(Password))
        {
            yield return new ValidationResult(
                "Password is required when Username is set.",
                [nameof(Password)]
            );
        }
    }
}
