using System.Net;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Wwg.Api.IntegrationTests.Support;

namespace Wwg.Api.IntegrationTests;

/// <summary>
/// Every endpoint that isn't anonymous answers 401 without a token (DESIGN §3.8), checked against
/// the app's real endpoint list, so a new endpoint is covered without a test of its own.
/// </summary>
public sealed class SignInRequiredTests : ApiTest
{
    [Fact]
    public async Task SignedInEndpoints_WithoutAToken_Return401()
    {
        var endpoints = App
            .Services.GetRequiredService<EndpointDataSource>()
            .Endpoints.OfType<RouteEndpoint>()
            // Fallbacks (the front-end's index.html, and plain 404s) catch everything else.
            .Where(e => e.Order != int.MaxValue)
            .Where(e =>
                e.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) ?? false
            )
            .Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .ToList();
        Assert.NotEmpty(endpoints);

        var wrong = new List<string>();
        foreach (var endpoint in endpoints)
        {
            var method =
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Single() ?? "GET";
            var path = PathFor(endpoint.RoutePattern.RawText ?? "");
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = method is "POST" or "PUT"
                    ? new StringContent("{}", Encoding.UTF8, "application/json")
                    : null,
            };
            using var response = await Client.SendAsync(request, CancellationToken);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                wrong.Add($"{method} {path}: {(int)response.StatusCode}");
            }
        }

        Assert.Empty(wrong);
    }

    /// <summary>A concrete path for a route pattern: any GUID for an ID, any text otherwise.</summary>
    private static string PathFor(string pattern) =>
        System.Text.RegularExpressions.Regex.Replace(
            pattern,
            "{[^}:]+(?<guid>:guid)?}",
            match => match.Groups["guid"].Success ? Guid.CreateVersion7().ToString() : "anything",
            System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
            TimeSpan.FromSeconds(1)
        );
}
