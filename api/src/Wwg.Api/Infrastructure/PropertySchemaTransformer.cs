using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Fixes to property schemas so the document (and the SDK's generated Zod schemas) describes
/// exactly what the API accepts.
/// </summary>
internal sealed class PropertySchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (context.JsonPropertyInfo is not { AttributeProvider: { } attributes } property)
        {
            return Task.CompletedTask;
        }

        // A custom JSON converter hides a property's type from the schema generator, so put back
        // the string type that [Trimmed] properties always have.
        if (attributes.IsDefined(typeof(TrimmedAttribute), false))
        {
            schema.Type = property.IsSetNullable
                ? JsonSchemaType.String | JsonSchemaType.Null
                : JsonSchemaType.String;
        }

        // [Required] rejects an empty string, but the document only marks the property as
        // required (present). Say it must be non-empty too, so client validation agrees.
        var required = attributes
            .GetCustomAttributes(typeof(RequiredAttribute), false)
            .OfType<RequiredAttribute>()
            .FirstOrDefault();
        if (
            required is { AllowEmptyStrings: false }
            && schema.Type?.HasFlag(JsonSchemaType.String) == true
            && schema.MinLength is null or 0
        )
        {
            schema.MinLength = 1;
        }

        return Task.CompletedTask;
    }
}
