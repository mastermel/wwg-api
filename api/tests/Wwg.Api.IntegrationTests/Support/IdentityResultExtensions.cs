using Microsoft.AspNetCore.Identity;

namespace Wwg.Api.IntegrationTests.Support;

internal static class IdentityResultExtensions
{
    public static void EnsureSucceeded(this IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(e => $"{e.Code}: {e.Description}"))
            );
        }
    }
}
