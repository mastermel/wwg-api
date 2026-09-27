namespace Wwg.Api.Infrastructure;

internal static class WebApplicationExtensions
{
    /// <summary>Configures the HTTP request pipeline (middleware order matters).</summary>
    public static WebApplication UseApiPipeline(this WebApplication app)
    {
        app.UseErrorHandling();

        return app;
    }
}
