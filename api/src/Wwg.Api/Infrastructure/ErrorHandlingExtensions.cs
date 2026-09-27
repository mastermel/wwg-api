using System.Text.Json;

namespace Wwg.Api.Infrastructure;

/// <summary>Every error response is RFC 9457 Problem Details (<c>application/problem+json</c>).</summary>
internal static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        return services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
            {
                var request = context.HttpContext.Request;
                context.ProblemDetails.Instance ??= $"{request.Method} {request.Path}";

                if (context.ProblemDetails is HttpValidationProblemDetails validation)
                {
                    CamelCaseErrorKeys(validation.Errors);
                }
            }
        );
    }

    /// <summary>
    /// Unhandled exceptions become a 500, and empty 4xx/5xx responses (e.g. an unknown route's
    /// 404) get a Problem Details body instead of an empty one.
    /// </summary>
    public static IApplicationBuilder UseErrorHandling(this IApplicationBuilder app)
    {
        return app.UseExceptionHandler().UseStatusCodePages();
    }

    /// <summary>
    /// Validation reports C# property names (<c>Items[0].Name</c>); rename them to match the JSON
    /// the client sent (<c>items[0].name</c>), so the front-end can map errors to fields.
    /// </summary>
    private static void CamelCaseErrorKeys(IDictionary<string, string[]> errors)
    {
        foreach (var (key, messages) in errors.ToList())
        {
            var camelKey = string.Join(
                '.',
                key.Split('.').Select(segment => JsonNamingPolicy.CamelCase.ConvertName(segment))
            );
            if (!string.Equals(camelKey, key, StringComparison.Ordinal))
            {
                errors.Remove(key);
                errors[camelKey] = messages;
            }
        }
    }
}
