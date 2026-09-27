using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Infrastructure;

internal static class ServiceCollectionExtensions
{
    /// <summary>Registers the services the API depends on.</summary>
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddValidatedOptions<AppOptions>(AppOptions.SectionName);
        services.AddSingleton(TimeProvider.System);
        services.AddHealthChecks();
        services.AddValidation();

        return services
            .AddErrorHandling()
            .AddJsonOptions()
            .AddTrustedForwardedHeaders()
            .AddApiDocument()
            .AddDatabase()
            .AddAuth();
    }
}
