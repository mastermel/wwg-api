using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.StaticFiles;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Serves the built front-end (web/dist, copied into wwwroot by the Docker build) from the same
/// origin as the API. Skipped when wwwroot/index.html doesn't exist (development and tests).
/// </summary>
internal static class SpaHostingExtensions
{
    /// <summary>Paths that belong to the API, never to the front-end, even if unknown.</summary>
    private static readonly string[] ServerPrefixes = ["/api", "/openapi", "/health"];

    /// <summary>
    /// Root files that must be revalidated on every load, or a new deploy wouldn't be picked up
    /// (a cached service worker would hold back updates).
    /// </summary>
    private static readonly string[] NoCachePaths =
    [
        "/index.html",
        "/sw.js",
        "/registerSW.js",
        "/manifest.webmanifest",
    ];

    /// <summary>
    /// Script is strict (only our own files). Style needs 'unsafe-inline': Mantine injects its CSS
    /// variables in a style element and uses style attributes. The campaign map (DESIGN.md §3.13)
    /// fetches tiles and fonts from OpenFreeMap and elevation from Mapterhorn, and MapLibre (and
    /// maplibre-contour) run web workers and decode images from blob: URLs.
    /// </summary>
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data: blob:; font-src 'self'; "
        + "connect-src 'self' https://tiles.openfreemap.org https://tiles.mapterhorn.com; "
        + "worker-src 'self' blob:; "
        + "manifest-src 'self'; object-src 'none'; base-uri 'self'; form-action 'self'; "
        + "frame-ancestors 'none'";

    private static bool IsEnabled(IWebHostEnvironment environment) =>
        environment.WebRootFileProvider.GetFileInfo("index.html").Exists;

    /// <summary>Static files, with caching and security headers. Call early in the pipeline.</summary>
    public static WebApplication UseSpaStaticFiles(this WebApplication app)
    {
        if (IsEnabled(app.Environment))
        {
            app.UseStaticFiles(CreateStaticFileOptions());
        }

        return app;
    }

    /// <summary>
    /// Unknown API paths get a Problem Details 404 (never index.html with a 200), and every other
    /// non-file URL gets index.html, so client-side routes work on reload. Call after mapping the
    /// API's endpoints.
    /// </summary>
    public static WebApplication MapSpaFallback(this WebApplication app)
    {
        foreach (var prefix in ServerPrefixes)
        {
            app.Map($"{prefix}/{{**path}}", NotFound).ExcludeFromDescription().AllowAnonymous();
        }

        // Requests that match nothing still pass through authorization, where the
        // sign-in-by-default policy would answer 401; these make them a plain 404.
        if (IsEnabled(app.Environment))
        {
            app.MapFallbackToFile("index.html", CreateStaticFileOptions())
                .AllowAnonymous()
                .ExcludeFromDescription();
            app.MapFallback("{*path:file}", NotFound).AllowAnonymous().ExcludeFromDescription();
        }
        else
        {
            app.MapFallback("{**path}", NotFound).AllowAnonymous().ExcludeFromDescription();
        }

        return app;
    }

    private static ProblemHttpResult NotFound() => TypedResults.Problem(statusCode: 404);

    private static StaticFileOptions CreateStaticFileOptions()
    {
        var contentTypes = new FileExtensionContentTypeProvider();
        contentTypes.Mappings[".webmanifest"] = "application/manifest+json";

        return new StaticFileOptions
        {
            ContentTypeProvider = contentTypes,
            OnPrepareResponse = context =>
            {
                var headers = context.Context.Response.Headers;
                var path = context.Context.Request.Path;

                headers.CacheControl =
                    path.StartsWithSegments("/assets", StringComparison.Ordinal)
                        // Vite's content-hashed files never change.
                        ? "public, max-age=31536000, immutable"
                    : NoCachePaths.Contains(path.Value, StringComparer.Ordinal)
                    || !Path.HasExtension(path.Value)
                        ? "no-cache"
                    : "public, max-age=86400";

                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.XContentTypeOptions = "nosniff";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            },
        };
    }
}
