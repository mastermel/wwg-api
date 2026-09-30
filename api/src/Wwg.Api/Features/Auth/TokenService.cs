using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

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

    /// <summary>
    /// Signs the user in: returns an access token and sets the refresh cookie. With a
    /// <paramref name="masquerade"/>, the session is that masquerade (decision 0012): its claims
    /// ride along, and neither token outlives it. Pass the current one on whenever tokens are
    /// reissued, or the session would quietly become the user's own.
    /// </summary>
    public async Task<TokenResponse> IssueAsync(
        HttpContext httpContext,
        AppUser user,
        Masquerade? masquerade = null
    )
    {
        var principal = await signInManager.CreateUserPrincipalAsync(user);
        if (masquerade is not null && principal.Identity is ClaimsIdentity identity)
        {
            identity.AddClaims(masquerade.ToClaims());
        }

        var now = timeProvider.GetUtcNow();
        var options = Options;
        var ends = masquerade?.Ends ?? DateTimeOffset.MaxValue;
        var accessLifetime = Shortest(options.BearerTokenExpiration, ends - now);
        var refreshLifetime = Shortest(options.RefreshTokenExpiration, ends - now);

        var accessToken = options.BearerTokenProtector.Protect(
            CreateTicket(principal, now + accessLifetime)
        );
        var refreshToken = options.RefreshTokenProtector.Protect(
            CreateTicket(principal, now + refreshLifetime)
        );

        httpContext.Response.Cookies.Append(
            RefreshCookieName,
            refreshToken,
            CookieOptions(maxAge: refreshLifetime)
        );

        return new TokenResponse(accessToken, (int)accessLifetime.TotalSeconds);
    }

    /// <summary>
    /// The Admin behind a masquerade, if it may go on: it hasn't ended, and they still exist, are
    /// still an Admin and have the same security stamp (no password change or sign out everywhere
    /// since). Null if not.
    /// </summary>
    public async Task<AppUser?> MasqueradingAdminAsync(Masquerade masquerade)
    {
        if (masquerade.Ends <= timeProvider.GetUtcNow())
        {
            return null;
        }

        var users = signInManager.UserManager;
        var admin = await users.FindByIdAsync(masquerade.AdminId.ToString());
        return
            admin is not null
            && string.Equals(admin.SecurityStamp, masquerade.AdminStamp, StringComparison.Ordinal)
            && await users.IsInRoleAsync(admin, Roles.Admin)
            ? admin
            : null;
    }

    private static TimeSpan Shortest(TimeSpan lifetime, TimeSpan left) =>
        left < lifetime ? left : lifetime;

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
        httpContext.Response.Cookies.Delete(RefreshCookieName, CookieOptions(maxAge: null));
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

    // Max-Age rather than Expires: the browser measures it on its own clock, so the cookie's
    // lifetime doesn't depend on the server's and client's clocks agreeing.
    private static CookieOptions CookieOptions(TimeSpan? maxAge) =>
        new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = RefreshCookiePath,
            MaxAge = maxAge,
            IsEssential = true,
        };
}
