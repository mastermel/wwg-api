using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace Wwg.Api.Features.Campaigns;

internal static class JoinCodes
{
    /// <summary>A new join code: 128 random bits, base64url (22 characters, safe in a URL).</summary>
    public static string Generate() =>
        WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(16));
}
