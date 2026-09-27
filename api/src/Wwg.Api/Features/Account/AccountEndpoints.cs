using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Account;

internal static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/api/me").WithTags("Account").RequireSignedIn();

        account.MapGet("", GetMeAsync).WithName("GetMe");

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
        if (user is null)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(
            new MeResponse(
                user.Id,
                user.Email ?? "",
                user.FirstName,
                user.LastName,
                principal.IsInRole(Roles.Admin)
            )
        );
    }
}
