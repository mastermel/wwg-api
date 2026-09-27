using System.ComponentModel.DataAnnotations;
using System.Net;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// The reverse proxies trusted to set <c>X-Forwarded-For</c> / <c>X-Forwarded-Proto</c>
/// (<c>ForwardedHeaders</c> section). Loopback is always trusted.
/// </summary>
internal sealed class ProxyOptions : IValidatableObject
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Proxy IP addresses, e.g. <c>172.18.0.1</c>.</summary>
    public IReadOnlyList<string> KnownProxies { get; set; } = [];

    /// <summary>Proxy networks in CIDR notation, e.g. <c>172.18.0.0/16</c>.</summary>
    public IReadOnlyList<string> KnownNetworks { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        foreach (var proxy in KnownProxies.Where(p => !IPAddress.TryParse(p, out _)))
        {
            yield return new ValidationResult(
                $"'{proxy}' is not an IP address.",
                [nameof(KnownProxies)]
            );
        }

        foreach (var network in KnownNetworks.Where(n => !IPNetwork.TryParse(n, out _)))
        {
            yield return new ValidationResult(
                $"'{network}' is not a network in CIDR notation.",
                [nameof(KnownNetworks)]
            );
        }
    }
}
