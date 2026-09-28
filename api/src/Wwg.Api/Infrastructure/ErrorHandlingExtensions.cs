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
        return app.UseExceptionHandler(
                new ExceptionHandlerOptions
                {
                    // A body that can't be read (bad JSON, a missing [JsonRequired] field) is the
                    // client's mistake. In Development minimal APIs throw for it rather than
                    // answer 400, which would otherwise become a 500 here.
                    StatusCodeSelector = exception =>
                        exception is BadHttpRequestException badRequest
                            ? badRequest.StatusCode
                            : StatusCodes.Status500InternalServerError,
                }
            )
            .UseStatusCodePages();
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
