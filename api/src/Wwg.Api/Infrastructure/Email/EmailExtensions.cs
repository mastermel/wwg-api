using Microsoft.Extensions.Options;

namespace Wwg.Api.Infrastructure.Email;

internal static class EmailExtensions
{
    public static IServiceCollection AddEmail(this IServiceCollection services)
    {
        services.AddValidatedOptions<SmtpOptions>(SmtpOptions.SectionName);

        services.AddSingleton<MailKitEmailService>();
        services.AddSingleton<LoggingEmailService>();
        services.AddSingleton<IEmailService>(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value.IsConfigured
                ? serviceProvider.GetRequiredService<MailKitEmailService>()
                : serviceProvider.GetRequiredService<LoggingEmailService>()
        );

        services.AddSingleton<EmailQueue>();
        services.AddSingleton<IEmailQueue>(serviceProvider =>
            serviceProvider.GetRequiredService<EmailQueue>()
        );
        services.AddHostedService(serviceProvider =>
            serviceProvider.GetRequiredService<EmailQueue>()
        );

        return services;
    }
}
