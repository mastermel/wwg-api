using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Behind the reverse proxy, takes the client IP (for per-IP rate limiting) and scheme (for
/// generated URLs) from the forwarded headers, but only from trusted proxies.
/// </summary>
internal static class ForwardedHeadersExtensions
{
    public static IServiceCollection AddTrustedForwardedHeaders(this IServiceCollection services)
    {
        services.AddValidatedOptions<ProxyOptions>(ProxyOptions.SectionName);

        services
            .AddOptions<ForwardedHeadersOptions>()
            .Configure<IOptions<ProxyOptions>>(
                (options, proxy) =>
                {
                    options.ForwardedHeaders =
                        ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

                    foreach (var address in proxy.Value.KnownProxies)
                    {
                        options.KnownProxies.Add(IPAddress.Parse(address));
                    }

                    foreach (var network in proxy.Value.KnownNetworks)
                    {
                        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
                    }
                }
            );

        return services;
    }
}
