using System.Text.Encodings.Web;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Account;

/// <summary>
/// Sent to the old address when the email changes, so a takeover (someone else changing it)
/// doesn't go unnoticed: emails aren't verified, so this is the safeguard.
/// </summary>
internal static class EmailChangedEmail
{
    public static EmailMessage Create(AppUser user, string oldEmail, string newEmail)
    {
        var name = HtmlEncoder.Default.Encode(user.FirstName);
        var encodedNew = HtmlEncoder.Default.Encode(newEmail);

        return new EmailMessage(
            ToAddress: oldEmail,
            ToName: $"{user.FirstName} {user.LastName}",
            Subject: "Your WWG Campaigner email was changed",
            HtmlBody: $"""
            <p>Hi {name},</p>
            <p>The email for your WWG Campaigner account was just changed to
            <strong>{encodedNew}</strong>. You'll sign in with the new address from now on.</p>
            <p>If you didn't do this, someone else may have your password: contact a club Admin
            straight away.</p>
            """,
            TextBody: $"""
            Hi {user.FirstName},

            The email for your WWG Campaigner account was just changed to {newEmail}.
            You'll sign in with the new address from now on.

            If you didn't do this, someone else may have your password: contact a club Admin
            straight away.
            """
        );
    }
}
