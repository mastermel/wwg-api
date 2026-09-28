using Microsoft.AspNetCore.Identity;

namespace Wwg.Api.Infrastructure.Auth;

internal static class IdentityResultExtensions
{
    /// <summary>For operations that can only fail through a bug or the database, not the user.</summary>
    public static void ThrowIfFailed(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity operation failed: "
                    + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"))
            );
        }
    }
}
