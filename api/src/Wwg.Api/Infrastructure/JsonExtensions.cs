using System.Text.Json.Serialization;

namespace Wwg.Api.Infrastructure;

internal static class JsonExtensions
{
    /// <summary>
    /// camelCase names (the web default), enums as strings and strict numbers, for request and
    /// response bodies and the OpenAPI document.
    /// </summary>
    public static IServiceCollection AddJsonOptions(this IServiceCollection services)
    {
        return services.ConfigureHttpJsonOptions(options =>
        {
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
            // Numbers are numbers. The web default also accepts them as strings, which makes
            // every number "integer or string" in the OpenAPI document and the SDK's types.
            options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
        });
    }
}
