using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure.Auth;

/// <summary>
/// Email confirmation links (decision 0023), apart from password resets: a welcome may sit unread
/// for days, where a reset link should die within hours.
/// </summary>
internal sealed class EmailConfirmationTokenProvider(
    IDataProtectionProvider dataProtectionProvider,
    IOptions<EmailConfirmationTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<AppUser>> logger
) : DataProtectorTokenProvider<AppUser>(dataProtectionProvider, options, logger)
{
    public const string ProviderName = "EmailConfirmation";
}

/// <summary>The confirmation links' own name and lifetime.</summary>
internal sealed class EmailConfirmationTokenProviderOptions : DataProtectionTokenProviderOptions
{
    public EmailConfirmationTokenProviderOptions()
    {
        Name = EmailConfirmationTokenProvider.ProviderName;
    }
}
