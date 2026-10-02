using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Auth;

namespace Wwg.Api.Infrastructure.Auth;

internal static class AuthExtensions
{
    /// <summary>
    /// ASP.NET Core Identity (user store, hashing, lockout, security stamps) with bearer tokens,
    /// behind our own endpoints (DESIGN.md §3.4).
    /// </summary>
    public static IServiceCollection AddAuth(this IServiceCollection services)
    {
        services.AddValidatedOptions<AuthOptions>(AuthOptions.SectionName);
        services.AddValidatedOptions<AdminOptions>(AdminOptions.SectionName);

        AddIdentity(services);

        services
            .AddAuthentication(IdentityConstants.BearerScheme)
            .AddBearerToken(IdentityConstants.BearerScheme);
        services
            .AddOptions<BearerTokenOptions>(IdentityConstants.BearerScheme)
            .Configure<IOptions<AuthOptions>>(
                (bearer, auth) =>
                {
                    bearer.BearerTokenExpiration = auth.Value.AccessTokenLifetime;
                    bearer.RefreshTokenExpiration = auth.Value.RefreshTokenLifetime;
                }
            );

        services.AddAuthorizationBuilder().AddAccessPolicies();
        services.AddScoped<IAuthorizationHandler, LibraryEditorHandler>();
        services.AddScoped<TokenService>();

        AddPersistedDataProtectionKeys(services);

        return services;
    }

    private static void AddIdentity(IServiceCollection services)
    {
        services
            .AddIdentityCore<AppUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                // The username is the email; the default character list rejects valid addresses
                // such as o'brien@example.com. Email format is validated on the request instead.
                options.User.AllowedUserNameCharacters = "";
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Tokens.EmailConfirmationTokenProvider =
                    EmailConfirmationTokenProvider.ProviderName;
                // Lockout keeps Identity's defaults: 5 failed attempts, 5 minutes.
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<WwgDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddTokenProvider<EmailConfirmationTokenProvider>(
                EmailConfirmationTokenProvider.ProviderName
            );

        services
            .AddOptions<DataProtectionTokenProviderOptions>()
            .Configure<IOptions<AuthOptions>>(
                (tokens, auth) => tokens.TokenLifespan = auth.Value.PasswordResetLinkLifetime
            );
        services
            .AddOptions<EmailConfirmationTokenProviderOptions>()
            .Configure<IOptions<AuthOptions>>(
                (tokens, auth) => tokens.TokenLifespan = auth.Value.EmailConfirmationLinkLifetime
            );
    }

    private static void AddPersistedDataProtectionKeys(IServiceCollection services)
    {
        // Keys persisted to a directory (the /data volume in production), like
        // PersistKeysToFileSystem, but with the path read from validated options.
        services.AddDataProtection().SetApplicationName("wwg");
        services
            .AddOptions<KeyManagementOptions>()
            .Configure<IOptions<AuthOptions>, IHostEnvironment, ILoggerFactory>(
                (keys, auth, environment, loggerFactory) =>
                    keys.XmlRepository = new FileSystemXmlRepository(
                        new DirectoryInfo(
                            Path.Combine(
                                environment.ContentRootPath,
                                auth.Value.DataProtectionKeysPath ?? "keys"
                            )
                        ),
                        loggerFactory
                    )
            );
    }
}
