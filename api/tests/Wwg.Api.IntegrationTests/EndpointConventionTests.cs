using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

/// <summary>
/// Rules every endpoint must follow, checked against the app's real endpoint list so they don't
/// depend on review. Each test lists all the endpoints that break its rule.
/// </summary>
public sealed class EndpointConventionTests : ApiTest
{
    /// <summary>Routes that are deliberately outside <c>/api</c>.</summary>
    private static readonly string[] NonApiPrefixes = ["/health", "/openapi/"];

    [Fact]
    public void Endpoints_InTheOpenApiDocument_HaveANameAndTag()
    {
        // The name is the operationId (the SDK's function name); the tag groups its files.
        var violations = DocumentedEndpoints()
            .Where(e =>
                e.Metadata.GetMetadata<IEndpointNameMetadata>() is null
                || e.Metadata.GetMetadata<ITagsMetadata>() is not { Tags.Count: > 0 }
            )
            .Select(Describe);

        Assert.Empty(violations);
    }

    [Fact]
    public void Endpoints_Always_DeclareAnAccessRule()
    {
        // Sign-in is required by default, but every endpoint must say which rule it means
        // (DESIGN.md §3.5), so a forgotten rule can't silently fall back to it.
        var violations = Endpoints()
            .Where(e =>
                e.Metadata.GetMetadata<IAllowAnonymous>() is null
                && e.Metadata.GetMetadata<AccessRuleMetadata>() is null
            )
            .Select(Describe);

        Assert.Empty(violations);
    }

    [Fact]
    public void Endpoints_UnderApiAdmin_AreAllAdminOnly()
    {
        var violations = Endpoints()
            .Where(e => Route(e).StartsWith("/api/admin", StringComparison.Ordinal))
            .Where(e =>
                !string.Equals(
                    e.Metadata.GetMetadata<AccessRuleMetadata>()?.Rule,
                    "admin",
                    StringComparison.Ordinal
                )
            )
            .Select(Describe);

        Assert.Empty(violations);
    }

    [Fact]
    public void Endpoints_Always_AreUnderApi()
    {
        var violations = Endpoints()
            // Fallbacks (the front-end's index.html, and plain 404s) catch everything else.
            .Where(e => e.Order != int.MaxValue)
            .Where(e =>
                !Route(e).StartsWith("/api/", StringComparison.Ordinal)
                && !NonApiPrefixes.Any(prefix =>
                    Route(e).StartsWith(prefix, StringComparison.Ordinal)
                )
            )
            .Select(Describe);

        Assert.Empty(violations);
    }

    private List<RouteEndpoint> Endpoints()
    {
        var endpoints = App
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            .ToList();

        // Guard against the checks passing because nothing was found.
        Assert.NotEmpty(endpoints);
        return endpoints;
    }

    private IEnumerable<RouteEndpoint> DocumentedEndpoints()
    {
        return Endpoints()
            .Where(e =>
                e.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>()
                    is not { ExcludeFromDescription: true }
            );
    }

    private static string Route(RouteEndpoint endpoint)
    {
        return "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
    }

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? [];
        return $"{string.Join(',', methods)} {Route(endpoint)}";
    }
}
