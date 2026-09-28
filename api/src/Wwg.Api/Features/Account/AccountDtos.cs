using System.ComponentModel.DataAnnotations;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Account;

/// <summary>The signed-in user.</summary>
/// <param name="Id">The user's ID.</param>
/// <param name="Email">Their email, which is also what they sign in with.</param>
/// <param name="FirstName">First name.</param>
/// <param name="LastName">Last name.</param>
/// <param name="IsAdmin">Whether they're a site-wide Admin.</param>
public sealed record MeResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsAdmin
);

/// <summary>Updates the signed-in user's name.</summary>
public sealed record UpdateProfileRequest(
    [property: Trimmed, Required, StringLength(100)] string FirstName,
    [property: Trimmed, Required, StringLength(100)] string LastName
);

/// <summary>Changes the password. Other sessions are signed out; this one continues.</summary>
public sealed record ChangePasswordRequest(
    [property: Required, StringLength(128)] string CurrentPassword,
    [property: Required, StringLength(128, MinimumLength = 8)] string NewPassword
);
