using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure.Auth;

internal static class SecurityStampMiddleware
{
    /// <summary>
    /// Checks every authenticated request's security stamp against the database (one indexed
    /// lookup). Bearer tokens alone aren't, so a deleted account or changed password would keep
    /// working until the token expired. A failed check makes the request anonymous, which the
    /// authorization step then answers with a 401.
    /// </summary>
    public static IApplicationBuilder UseSecurityStampValidation(this IApplicationBuilder app) =>
        app.Use(
            async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    var signInManager = context.RequestServices.GetRequiredService<
                        SignInManager<AppUser>
                    >();
                    if (await signInManager.ValidateSecurityStampAsync(context.User) is null)
                    {
                        context.User = new ClaimsPrincipal(new ClaimsIdentity());
                    }
                }

                await next(context);
            }
        );
}
