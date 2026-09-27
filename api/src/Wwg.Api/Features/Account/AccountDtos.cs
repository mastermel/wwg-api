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
