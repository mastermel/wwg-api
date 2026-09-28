using System.Text.Encodings.Web;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Auth;

internal static class PasswordResetEmail
{
    public static EmailMessage Create(AppUser user, Uri resetLink)
    {
        var name = HtmlEncoder.Default.Encode(user.FirstName);
        var link = HtmlEncoder.Default.Encode(resetLink.ToString());

        return new EmailMessage(
            ToAddress: user.Email ?? "",
            ToName: $"{user.FirstName} {user.LastName}",
            Subject: "Reset your WWG Campaigner password",
            HtmlBody: $"""
            <p>Hi {name},</p>
            <p>Someone asked to reset the password for your WWG Campaigner account.
            If it was you, choose a new password here (the link works for 2 hours):</p>
            <p><a href="{link}">Reset my password</a></p>
            <p>If it wasn't you, you can ignore this email; your password hasn't changed.</p>
            """,
            TextBody: $"""
            Hi {user.FirstName},

            Someone asked to reset the password for your WWG Campaigner account.
            If it was you, choose a new password here (the link works for 2 hours):

            {resetLink}

            If it wasn't you, you can ignore this email; your password hasn't changed.
            """
        );
    }
}
