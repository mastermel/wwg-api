using Microsoft.Extensions.Options;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// The OpenAPI document (the contract the front-end SDK is generated from) and Swagger UI.
/// </summary>
internal static class OpenApiExtensions
{
    public static IServiceCollection AddApiDocument(this IServiceCollection services)
    {
        return services.AddOpenApi(options =>
            options
                .AddDocumentTransformer(
                    (document, _, _) =>
                    {
                        document.Info.Title = "wwg API";
                        document.Info.Version = "v1";
                        document.Info.Description = "The Wasatch Wargamers Campaign App API.";
                        // The API is always called on the same origin; a server URL would also make
                        // the build-time document depend on where it was generated.
                        document.Servers = [];
                        return Task.CompletedTask;
                    }
                )
                .AddSchemaTransformer<PropertySchemaTransformer>()
                .AddDocumentTransformer<BearerSecurityTransformers>()
                .AddOperationTransformer<BearerSecurityTransformers>()
        );
    }

    /// <summary>
    /// Serves the document at <c>/openapi/v1.json</c>. Swagger UI (<c>/swagger</c>) is added by
    /// <see cref="UseSwaggerUiIfEnabled"/>, in development or when
    /// <see cref="AppOptions.EnableSwaggerUi"/> is set.
    /// </summary>
    public static WebApplication MapApiDocument(this WebApplication app)
    {
        app.MapOpenApi().AllowAnonymous();
        return app;
    }

    /// <summary>
    /// Swagger UI is middleware, not an endpoint, so it must come before authorization (the
    /// sign-in-by-default policy would otherwise answer it with a 401).
    /// </summary>
    public static WebApplication UseSwaggerUiIfEnabled(this WebApplication app)
    {
        // Reading the options validates them, and there are no real settings at build time.
        if (BuildTime.IsGeneratingOpenApiDocument)
        {
            return app;
        }

        var appOptions = app.Services.GetRequiredService<IOptions<AppOptions>>().Value;
        if (app.Environment.IsDevelopment() || appOptions.EnableSwaggerUi)
        {
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/openapi/v1.json", "wwg API v1");
                options.DocumentTitle = "wwg API";
            });
        }

        return app;
    }
}
