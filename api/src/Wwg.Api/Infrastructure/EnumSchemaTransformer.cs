using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Wwg.Api.Infrastructure;

/// <summary>
/// Keeps null out of an enum's own values. The document describes an enum once, from the first
/// property of its type it meets; when that one is nullable (a turn's time of day, say), null
/// joins the enum's values, and the SDK then types every property of it as nullable. A nullable
/// property says so itself, alongside the enum (oneOf null).
/// </summary>
internal sealed class EnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        if (schema.Enum is { Count: > 0 } values)
        {
            schema.Enum = [.. values.Where(value => value is not null)];
        }

        return Task.CompletedTask;
    }
}
