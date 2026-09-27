using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Infrastructure;

internal static class WebApplicationExtensions
{
    /// <summary>Configures the HTTP request pipeline (middleware order matters).</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // First, so everything after sees the real client IP and scheme.
        app.UseForwardedHeaders();
        app.UseErrorHandling();
        app.UseSpaStaticFiles();
        app.UseSwaggerUiIfEnabled();
        // Routing, authentication and authorization are called explicitly, after static files:
        // otherwise ASP.NET adds them at the start, an endpoint (e.g. the 404 fallback) is chosen
        // before static files run, and the sign-in-by-default policy applies to the front-end's
        // own files.
        app.UseRouting();
        app.UseAuthentication();
        app.UseSecurityStampValidation();
        app.UseAuthorization();
        app.MapApiDocument();

        return app;
    }
}
