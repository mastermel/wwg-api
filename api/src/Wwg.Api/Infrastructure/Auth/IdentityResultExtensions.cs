using Microsoft.AspNetCore.Identity;

namespace Wwg.Api.Infrastructure.Auth;

internal static class IdentityResultExtensions
{
    /// <summary>For operations that can only fail through a bug or the database, not the user.</summary>
    public static void ThrowIfFailed(this IdentityResult result)
    {
        result.ThrowIfConcurrencyFailure();
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Identity operation failed: "
                    + string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"))
            );
        }
    }

    /// <summary>
    /// Identity refused the save because another request changed the user since it was loaded
    /// (its ConcurrencyStamp): a 409, through <see cref="ConcurrentChangeException"/>.
    /// </summary>
    public static void ThrowIfConcurrencyFailure(this IdentityResult result)
    {
        if (
            result.Errors.Any(e =>
                string.Equals(
                    e.Code,
                    nameof(IdentityErrorDescriber.ConcurrencyFailure),
                    StringComparison.Ordinal
                )
            )
        )
        {
            throw new ConcurrentChangeException();
        }
    }
}
