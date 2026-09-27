using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Auth;

/// <summary>
/// Issues the token pair ourselves, with the bearer scheme's own protectors, so the refresh token
/// goes into a cookie instead of the JSON body that Identity's sign-in would write (decision 0004).
/// </summary>
internal sealed class TokenService(
    SignInManager<AppUser> signInManager,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    TimeProvider timeProvider
)
{
    public const string RefreshCookieName = "__Secure-wwg-refresh";
    public const string RefreshCookiePath = "/api/auth";

    private BearerTokenOptions Options => bearerOptions.Get(IdentityConstants.BearerScheme);

    /// <summary>Signs the user in: returns an access token and sets the refresh cookie.</summary>
    public async Task<TokenResponse> IssueAsync(HttpContext httpContext, AppUser user)
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        var now = timeProvider.GetUtcNow();
        var options = Options;

        var accessToken = options.BearerTokenProtector.Protect(
            CreateTicket(principal, now + options.BearerTokenExpiration)
        );
        var refreshExpires = now + options.RefreshTokenExpiration;
        var refreshToken = options.RefreshTokenProtector.Protect(
            CreateTicket(principal, refreshExpires)
        );

        httpContext.Response.Cookies.Append(
            RefreshCookieName,
            refreshToken,
            CookieOptions(refreshExpires)
        );

        return new TokenResponse(accessToken, (int)options.BearerTokenExpiration.TotalSeconds);
    }

    /// <summary>
    /// The principal inside a refresh token, or null if it's missing, can't be read or has
    /// expired. The caller still has to check the security stamp.
    /// </summary>
    public ClaimsPrincipal? ReadRefreshToken(HttpContext httpContext)
    {
        if (!httpContext.Request.Cookies.TryGetValue(RefreshCookieName, out var token))
        {
            return null;
        }

        var ticket = Options.RefreshTokenProtector.Unprotect(token);
        return ticket?.Properties.ExpiresUtc is { } expires && expires > timeProvider.GetUtcNow()
            ? ticket.Principal
            : null;
    }

    /// <summary>Removes the refresh cookie (script can't: it's HttpOnly).</summary>
    public static void ClearRefreshCookie(HttpContext httpContext)
    {
        httpContext.Response.Cookies.Delete(RefreshCookieName, CookieOptions(expires: null));
    }

    private static AuthenticationTicket CreateTicket(
        ClaimsPrincipal principal,
        DateTimeOffset expires
    ) =>
        new(
            principal,
            new AuthenticationProperties { ExpiresUtc = expires },
            IdentityConstants.BearerScheme
        );

    private static CookieOptions CookieOptions(DateTimeOffset? expires) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            Expires = expires,
            IsEssential = true,
        };
}
