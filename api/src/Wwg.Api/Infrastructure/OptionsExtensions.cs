using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure;

internal static class OptionsExtensions
{
    /// <summary>
    /// Binds a settings section to <typeparamref name="TOptions"/> and validates its
    /// DataAnnotations (and <see cref="System.ComponentModel.DataAnnotations.IValidatableObject"/>)
    /// at startup, so bad config fails fast with a clear message. Every settings section uses this.
    /// Startup validation is skipped while generating the OpenAPI document at build time.
    /// </summary>
    public static OptionsBuilder<TOptions> AddValidatedOptions<TOptions>(
        this IServiceCollection services,
        string sectionName
    )
        where TOptions : class
    {
        var options = services
            .AddOptions<TOptions>()
            .BindConfiguration(sectionName)
            .ValidateDataAnnotations();

        return BuildTime.IsGeneratingOpenApiDocument ? options : options.ValidateOnStart();
    }
}
