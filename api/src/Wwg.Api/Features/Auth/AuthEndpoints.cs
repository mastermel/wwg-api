using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Email;

namespace Wwg.Api.Features.Auth;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status409Conflict);
        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .RequireRateLimiting(RateLimiting.RefreshPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/logout", Logout).WithName("Logout");
        auth.MapPost("/forgot-password", ForgotPasswordAsync)
            .WithName("ForgotPassword")
            .RequireRateLimiting(RateLimiting.EmailPolicy)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
        auth.MapPost("/reset-password", ResetPasswordAsync)
            .WithName("ResetPassword")
            .RequireRateLimiting(RateLimiting.AuthPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        return app;
    }

    /// <summary>Creates an account and signs it in.</summary>
    internal static async Task<
        Results<Ok<TokenResponse>, ValidationProblem, ProblemHttpResult>
    > RegisterAsync(
        RegisterRequest request,
        UserManager<AppUser> userManager,
        TokenService tokens,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
        };

        cancellationToken.ThrowIfCancellationRequested();
        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            // The username is the email, so a duplicate shows up as either code.
            if (result.Errors.Any(e => e.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return TypedResults.Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Email already in use",
                    detail: "An account with this email already exists."
                );
            }

            return TypedResults.ValidationProblem(ToValidationErrors(result));
        }

        return TypedResults.Ok(await tokens.IssueAsync(httpContext, user));
    }

    /// <summary>Signs in with email and password. Repeated failures lock the account briefly.</summary>
    internal static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> LoginAsync(
        LoginRequest request,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager,
        TokenService tokens,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return IncorrectCredentials();
        }

        var result = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true
        );
        if (result.IsLockedOut)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Account locked",
                detail: "Too many failed sign-ins. Try again in a few minutes."
            );
        }

        return result.Succeeded
            ? TypedResults.Ok(await tokens.IssueAsync(httpContext, user))
            : IncorrectCredentials();
    }

    /// <summary>
    /// Swaps the refresh cookie for a new access token and a new refresh cookie (sliding: the 30
    /// days start again). Fails if the cookie is missing, unreadable or expired, or the account is
    /// gone or its security stamp changed (password change, sign out everywhere).
    /// </summary>
    internal static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> RefreshAsync(
        TokenService tokens,
        SignInManager<AppUser> signInManager,
        HttpContext httpContext,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var principal = tokens.ReadRefreshToken(httpContext);
        var user = principal is null
            ? null
            : await signInManager.ValidateSecurityStampAsync(principal);

        if (user is null)
        {
            TokenService.ClearRefreshCookie(httpContext);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Signed out",
                detail: "Your session has ended. Sign in again."
            );
        }

        return TypedResults.Ok(await tokens.IssueAsync(httpContext, user));
    }

    /// <summary>
    /// Signs out this browser by removing the refresh cookie. An access token already issued keeps
    /// working until it expires (at most 30 minutes); the app discards it.
    /// </summary>
    internal static NoContent Logout(HttpContext httpContext)
    {
        TokenService.ClearRefreshCookie(httpContext);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Emails a password reset link, if there's an account for the email. Always succeeds, so it
    /// can't be used to find out which emails have accounts.
    /// </summary>
    internal static async Task<NoContent> ForgotPasswordAsync(
        ForgotPasswordRequest request,
        UserManager<AppUser> userManager,
        IEmailQueue emails,
        IOptions<AppOptions> appOptions,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is not null)
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
            // From config, never the request's Host header, which an attacker could forge to
            // point the link at their own site.
            var link = new Uri(
                appOptions.Value.PublicUrl!, // Required and validated at startup.
                $"/reset-password?email={Uri.EscapeDataString(user.Email ?? "")}&code={code}"
            );
            await emails.QueueAsync(PasswordResetEmail.Create(user, link), cancellationToken);
        }

        return TypedResults.NoContent();
    }

    /// <summary>
    /// Sets a new password with the code from a reset link. This signs out every existing session
    /// and clears any sign-in lockout.
    /// </summary>
    internal static async Task<Results<NoContent, ValidationProblem>> ResetPasswordAsync(
        ResetPasswordRequest request,
        UserManager<AppUser> userManager,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(request.Email);
        var token = DecodeCode(request.Code);
        if (user is null || token is null)
        {
            // The same answer for an unknown email as for a bad code.
            return InvalidResetCode();
        }

        var result = await userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
        {
            return result.Errors.Any(e =>
                string.Equals(
                    e.Code,
                    nameof(IdentityErrorDescriber.InvalidToken),
                    StringComparison.Ordinal
                )
            )
                ? InvalidResetCode()
                : TypedResults.ValidationProblem(
                    new Dictionary<string, string[]>(StringComparer.Ordinal)
                    {
                        ["newPassword"] = [.. result.Errors.Select(e => e.Description)],
                    }
                );
        }

        await userManager.SetLockoutEndDateAsync(user, null);
        await userManager.ResetAccessFailedCountAsync(user);
        return TypedResults.NoContent();
    }

    private static string? DecodeCode(string code)
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

    private static ValidationProblem InvalidResetCode() =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["code"] = ["This reset link is invalid or has expired. Ask for a new one."],
            }
        );

    // The same answer for an unknown email and a wrong password, so sign-in can't be used to
    // find out which emails have accounts.
    private static ProblemHttpResult IncorrectCredentials() =>
        TypedResults.Problem(
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Sign-in failed",
            detail: "The email or password is incorrect."
        );

    /// <summary>Identity's errors, keyed by the request field they're about.</summary>
    private static Dictionary<string, string[]> ToValidationErrors(IdentityResult result) =>
        result
            .Errors.GroupBy(
                e => e.Code.StartsWith("Password", StringComparison.Ordinal) ? "password" : "email",
                StringComparer.Ordinal
            )
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.Description).ToArray(),
                StringComparer.Ordinal
            );
}
