using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

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
                .AddSchemaTransformer(
                    (schema, context, _) =>
                    {
                        // A custom JSON converter hides a property's type from the schema generator,
                        // so put back the string type that [Trimmed] properties always have.
                        if (
                            context.JsonPropertyInfo is { } property
                            && property.AttributeProvider?.IsDefined(
                                typeof(TrimmedAttribute),
                                false
                            ) == true
                        )
                        {
                            schema.Type = property.IsSetNullable
                                ? JsonSchemaType.String | JsonSchemaType.Null
                                : JsonSchemaType.String;
                        }

                        return Task.CompletedTask;
                    }
                )
        );
    }

    /// <summary>
    /// Serves the document at <c>/openapi/v1.json</c>, and Swagger UI at <c>/swagger</c> in
    /// development (or when <see cref="AppOptions.EnableSwaggerUi"/> is set).
    /// </summary>
    public static WebApplication MapApiDocument(this WebApplication app)
    {
        app.MapOpenApi();

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
