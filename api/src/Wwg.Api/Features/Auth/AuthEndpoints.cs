using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Auth;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth").AllowAnonymous();

        auth.MapPost("/register", RegisterAsync)
            .WithName("Register")
            .ProducesProblem(StatusCodes.Status409Conflict);
        auth.MapPost("/login", LoginAsync)
            .WithName("Login")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/refresh", RefreshAsync)
            .WithName("Refresh")
            .ProducesProblem(StatusCodes.Status401Unauthorized);
        auth.MapPost("/logout", Logout).WithName("Logout");

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
