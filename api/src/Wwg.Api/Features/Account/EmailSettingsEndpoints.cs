using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Account;

/// <summary>The campaign emails a user turns off (step 52a, decision 0023).</summary>
internal static class EmailSettingsEndpoints
{
    /// <summary>The campaign emails the signed-in user has turned off.</summary>
    internal static async Task<
        Results<Ok<EmailSettingsResponse>, UnauthorizedHttpResult>
    > GetEmailSettingsAsync(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        return user is null
            ? TypedResults.Unauthorized()
            : TypedResults.Ok(new EmailSettingsResponse([.. user.MutedEmails]));
    }

    /// <summary>Turns campaign emails off, or back on, for the signed-in user.</summary>
    internal static async Task<
        Results<Ok<EmailSettingsResponse>, ValidationProblem, UnauthorizedHttpResult>
    > UpdateEmailSettingsAsync(
        UpdateEmailSettingsRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Muted.Any(k => !Enum.IsDefined(k)))
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["muted"] = ["Choose emails from the list."],
                }
            );
        }

        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.MutedEmails = [.. request.Muted.Distinct().Order()];
        (await userManager.UpdateAsync(user)).ThrowIfFailed();
        return TypedResults.Ok(new EmailSettingsResponse([.. user.MutedEmails]));
    }
}
