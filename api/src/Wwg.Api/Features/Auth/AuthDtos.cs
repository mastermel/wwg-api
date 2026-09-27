using System.ComponentModel.DataAnnotations;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Auth;

/// <summary>Sign up. The account is signed in straight away.</summary>
public sealed record RegisterRequest(
    [property: Trimmed, Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128, MinimumLength = 8)] string Password,
    [property: Trimmed, Required, StringLength(100)] string FirstName,
    [property: Trimmed, Required, StringLength(100)] string LastName
);

/// <summary>Sign in with email and password.</summary>
public sealed record LoginRequest(
    [property: Trimmed, Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(128)] string Password
);

/// <summary>
/// A signed-in session. The access token goes in <c>Authorization: Bearer …</c>; the refresh
/// token is never in a body: it's set as an HttpOnly cookie that only /api/auth receives.
/// </summary>
/// <param name="AccessToken">The bearer token for API calls.</param>
/// <param name="ExpiresIn">Seconds until the access token expires; refresh before then.</param>
public sealed record TokenResponse(string AccessToken, int ExpiresIn);
