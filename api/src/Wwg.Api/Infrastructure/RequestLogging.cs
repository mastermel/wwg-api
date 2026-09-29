using Microsoft.AspNetCore.HttpLogging;

namespace Wwg.Api.Infrastructure;

internal static class RequestLogging
{
    /// <summary>
    /// One log line per API request: method, path, status and duration. Never headers, bodies or
    /// query strings, which can hold tokens, passwords and reset codes. Only <c>/api</c>: static
    /// files and the health check (Docker polls it) would drown out the rest.
    /// </summary>
    public static IServiceCollection AddRequestLogging(this IServiceCollection services)
    {
        services.AddHttpLogging(options =>
        {
            options.LoggingFields =
                HttpLoggingFields.RequestMethod
                | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode
                | HttpLoggingFields.Duration;
            options.CombineLogs = true;
        });
        services.AddSingleton<IHttpLoggingInterceptor, ApiRequestsOnly>();

        return services;
    }

    private sealed class ApiRequestsOnly : IHttpLoggingInterceptor
    {
        public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
        {
            if (
                !logContext.HttpContext.Request.Path.StartsWithSegments(
                    "/api",
                    StringComparison.OrdinalIgnoreCase
                )
            )
            {
                logContext.LoggingFields = HttpLoggingFields.None;
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext) =>
            ValueTask.CompletedTask;
    }
}
