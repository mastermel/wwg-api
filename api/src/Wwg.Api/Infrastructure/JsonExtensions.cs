using System.Text.Json.Serialization;

namespace Wwg.Api.Infrastructure;

internal static class JsonExtensions
{
    /// <summary>
    /// camelCase names (the web default) and enums as strings, for request/response bodies and
    /// the OpenAPI document.
    /// </summary>
    public static IServiceCollection AddJsonOptions(this IServiceCollection services)
    {
        return services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter())
        );
    }
}
