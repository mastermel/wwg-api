namespace Wwg.Api.Infrastructure;

/// <summary>Every error response is RFC 9457 Problem Details (<c>application/problem+json</c>).</summary>
internal static class ErrorHandlingExtensions
{
    public static IServiceCollection AddErrorHandling(this IServiceCollection services)
    {
        return services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Instance ??=
                    $"{context.HttpContext.Request.Method} {context.HttpContext.Request.Path}"
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
}
