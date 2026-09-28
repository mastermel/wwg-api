using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Auth;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Account;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/me").WithTags("Account").RequireSignedIn();

        account.MapGet("", GetMeAsync).WithName("GetMe");
        account.MapPut("", UpdateMeAsync).WithName("UpdateMe");
        account
            .MapPut("/password", ChangePasswordAsync)
            .WithName("ChangePassword")
            // A stolen access token mustn't allow quick guessing of the current password.
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        account
            .MapPut("/email", ChangeEmailAsync)
            .WithName("ChangeEmail")
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        account
            .MapPost("/sign-out-everywhere", SignOutEverywhereAsync)
            .WithName("SignOutEverywhere");

        return app;
    }

    /// <summary>The signed-in user's profile.</summary>
    internal static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> GetMeAsync(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        return user is null ? TypedResults.Unauthorized() : TypedResults.Ok(ToMe(user, principal));
    }

    /// <summary>Updates the signed-in user's first and last name.</summary>
    internal static async Task<Results<Ok<MeResponse>, UnauthorizedHttpResult>> UpdateMeAsync(
        UpdateProfileRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        (await userManager.UpdateAsync(user)).ThrowIfFailed();
        return TypedResults.Ok(ToMe(user, principal));
    }

    /// <summary>
    /// Changes the password (the current one is required). Every other session is signed out; the
    /// response carries new tokens so this one keeps working.
    /// </summary>
    internal static async Task<
        Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>
    > ChangePasswordAsync(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        TokenService tokens,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        var result = await userManager.ChangePasswordAsync(
            user,
            request.CurrentPassword,
            request.NewPassword
        );
        if (!result.Succeeded)
        {
            var wrongPassword = result.Errors.Any(e =>
                string.Equals(
                    e.Code,
                    nameof(IdentityErrorDescriber.PasswordMismatch),
                    StringComparison.Ordinal
                )
            );
            return TypedResults.ValidationProblem(
                wrongPassword
                    ? Errors("currentPassword", "The current password is incorrect.")
                    : Errors("newPassword", [.. result.Errors.Select(e => e.Description)])
            );
        }

        // The password change rotated the security stamp; these tokens carry the new one.
        return TypedResults.Ok(await tokens.IssueAsync(httpContext, user));
    }

    /// <summary>
    /// Changes the email the account signs in with (the current password is required). Every other
    /// session is signed out, a notice goes to the old address, and the response carries new
    /// tokens so this session keeps working.
    /// </summary>
    internal static async Task<
        Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult, UnauthorizedHttpResult>
    > ChangeEmailAsync(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        TokenService tokens,
        IEmailQueue emails,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        if (!await userManager.CheckPasswordAsync(user, request.CurrentPassword))
        {
            return TypedResults.ValidationProblem(
                Errors("currentPassword", "The current password is incorrect.")
            );
        }

        if (
            string.Equals(
                userManager.NormalizeEmail(request.NewEmail),
                user.NormalizedEmail,
                StringComparison.Ordinal
            )
        )
        {
            return TypedResults.ValidationProblem(Errors("newEmail", "That's already your email."));
        }

        var oldEmail = user.Email ?? "";
        // Email and username (which is the email) change together, with the new security stamp,
        // in one save: UpdateSecurityStampAsync validates (unique email) and saves.
        user.Email = request.NewEmail;
        user.UserName = request.NewEmail;
        await userManager.UpdateNormalizedEmailAsync(user);
        await userManager.UpdateNormalizedUserNameAsync(user);
        var result = await userManager.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName")
                ? TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Email already in use",
                    detail: "An account with this email already exists."
                )
                : TypedResults.ValidationProblem(
                    Errors("newEmail", [.. result.Errors.Select(e => e.Description)])
                );
        }

        await emails.QueueAsync(
            EmailChangedEmail.Create(user, oldEmail, request.NewEmail),
            cancellationToken
        );
        return TypedResults.Ok(await tokens.IssueAsync(httpContext, user));
    }

    /// <summary>
    /// Signs out every session, this one included: every access and refresh token stops working.
    /// </summary>
    internal static async Task<Results<NoContent, UnauthorizedHttpResult>> SignOutEverywhereAsync(
        ClaimsPrincipal principal,
        UserManager<AppUser> userManager,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.GetUserAsync(principal);
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        (await userManager.UpdateSecurityStampAsync(user)).ThrowIfFailed();
        TokenService.ClearRefreshCookie(httpContext);
        return TypedResults.NoContent();
    }

    private static MeResponse ToMe(AppUser user, ClaimsPrincipal principal) =>
        new(
            user.Id,
            user.Email ?? "",
            user.FirstName,
            user.LastName,
            principal.IsInRole(Roles.Admin)
        );

    private static Dictionary<string, string[]> Errors(string field, params string[] messages) =>
        new(StringComparer.Ordinal) { [field] = messages };
}
