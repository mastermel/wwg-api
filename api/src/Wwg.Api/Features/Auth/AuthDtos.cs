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

/// <summary>Asks for a password reset link by email.</summary>
public sealed record ForgotPasswordRequest(
    [property: Trimmed, Required, EmailAddress, StringLength(256)] string Email
);

/// <summary>Sets a new password with the code from a reset link.</summary>
public sealed record ResetPasswordRequest(
    [property: Trimmed, Required, EmailAddress, StringLength(256)] string Email,
    [property: Required, StringLength(2048)] string Code,
    [property: Required, StringLength(128, MinimumLength = 8)] string NewPassword
);

/// <summary>
/// A signed-in session. The access token goes in <c>Authorization: Bearer …</c>; the refresh
/// token is never in a body: it's set as an HttpOnly cookie that only /api/auth receives.
/// </summary>
/// <param name="AccessToken">The bearer token for API calls.</param>
/// <param name="ExpiresIn">Seconds until the access token expires; refresh before then.</param>
public sealed record TokenResponse(string AccessToken, int ExpiresIn);
