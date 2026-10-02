using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Auth;

/// <summary>
/// Confirming an account's address (step 52b, decision 0023): a welcome on registering, and a
/// link to the new address on changing it, each to the app's confirm page.
/// </summary>
internal static class EmailConfirmation
{
    /// <summary>Queues the email with a fresh link: a welcome, or a changed address's.</summary>
    public static async Task SendAsync(
        UserManager<AppUser> userManager,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        AppUser user,
        bool welcome,
        CancellationToken cancellationToken
    )
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        // From config, never the request's Host header (as the password reset's).
        var link = new Uri(
            appOptions.Value.PublicUrl!, // Required and validated at startup.
            $"/confirm-email?user={user.Id}&code={code}"
        );
        await emails.QueueAsync(Create(user, link, welcome), cancellationToken);
    }

    /// <summary>The token in a link's code, or null if it isn't one.</summary>
    public static string? TokenOf(string code)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static EmailMessage Create(AppUser user, Uri confirmLink, bool welcome)
    {
        var name = HtmlEncoder.Default.Encode(user.FirstName);
        var link = HtmlEncoder.Default.Encode(confirmLink.ToString());
        var (subject, opening) = welcome
            ? (
                "Welcome to Wasatch Wargamers: confirm your email",
                "Welcome to Wasatch Wargamers. Your account is ready; please confirm this is your email address, so the club's emails reach you."
            )
            : (
                "Confirm your new Wasatch Wargamers email",
                "Your Wasatch Wargamers account now signs in with this email address. Please confirm it's yours."
            );

        return new EmailMessage(
            ToAddress: user.Email ?? "",
            ToName: $"{user.FirstName} {user.LastName}",
            Subject: subject,
            HtmlBody: $"""
            <p>Hi {name},</p>
            <p>{HtmlEncoder.Default.Encode(opening)}</p>
            <p><a href="{link}">Confirm my email</a></p>
            <p>The link works for 7 days. If you didn't sign up, you can ignore this email.</p>
            """,
            TextBody: $"""
            Hi {user.FirstName},

            {opening}

            Confirm my email: {confirmLink}

            The link works for 7 days. If you didn't sign up, you can ignore this email.
            """
        );
    }
}
