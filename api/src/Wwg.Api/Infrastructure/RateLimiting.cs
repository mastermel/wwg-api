using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure;

/// <summary>Request limits per client IP (<c>RateLimits</c> section).</summary>
internal sealed class RateLimitOptions
{
    public const string SectionName = "RateLimits";

    /// <summary>Register, login, refresh and reset-password.</summary>
    [Required]
    public FixedWindowLimit Auth { get; set; } =
        new() { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) };

    /// <summary>
    /// Forgot-password, which sends an email: tight, so it can't be used to flood someone's inbox
    /// or run up SMTP costs.
    /// </summary>
    [Required]
    public FixedWindowLimit Email { get; set; } =
        new() { PermitLimit = 3, Window = TimeSpan.FromMinutes(15) };
}

internal sealed class FixedWindowLimit
{
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; }

    [Range(typeof(TimeSpan), "00:00:01", "1.00:00:00")]
    public TimeSpan Window { get; set; }
}

internal static class RateLimiting
{
    /// <summary>Policy for register, login, refresh and reset-password.</summary>
    public const string AuthPolicy = "auth";

    /// <summary>Policy for forgot-password.</summary>
    public const string EmailPolicy = "email";

    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        services.AddValidatedOptions<RateLimitOptions>(RateLimitOptions.SectionName);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = WriteRejectionAsync;

            options.AddPolicy(AuthPolicy, httpContext => PerClientIp(httpContext, o => o.Auth));
            options.AddPolicy(EmailPolicy, httpContext => PerClientIp(httpContext, o => o.Email));
        });
    }

    // Partitioned by client IP, which is the real one behind the proxy (forwarded headers).
    private static RateLimitPartition<string> PerClientIp(
        HttpContext httpContext,
        Func<RateLimitOptions, FixedWindowLimit> select
    )
    {
        var limit = select(
            httpContext.RequestServices.GetRequiredService<IOptions<RateLimitOptions>>().Value
        );
        return RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limit.PermitLimit,
                Window = limit.Window,
                QueueLimit = 0,
            }
        );
    }

    private static async ValueTask WriteRejectionAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken
    )
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(
                CultureInfo.InvariantCulture
            );
        }

        await context
            .HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(
                new ProblemDetailsContext
                {
                    HttpContext = context.HttpContext,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Too many requests",
                        Detail = "Too many attempts. Wait a moment and try again.",
                    },
                }
            );
    }
}
