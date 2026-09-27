namespace Wwg.Api.Infrastructure;

internal static class WebApplicationExtensions
{
    /// <summary>Configures the HTTP request pipeline (middleware order matters).</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        // First, so everything after sees the real client IP and scheme.
        app.UseForwardedHeaders();
        app.UseErrorHandling();

        return app;
    }
}
