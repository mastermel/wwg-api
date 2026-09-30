using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure.Auth;

internal static class Roles
{
    /// <summary>The site-wide Admin role (DESIGN.md §3.5), synced from Admin:Emails.</summary>
    public const string Admin = "Admin";

    /// <summary>Edits the library (decision 0015); Admins grant it on a user's admin page.</summary>
    public const string Manager = "Manager";
}

/// <summary>
/// Marks an endpoint's declared access rule. Sign-in is required by default (a fallback policy),
/// but every endpoint must still say which rule it means: AllowAnonymous, RequireSignedIn,
/// AdminOnly, RequireLibraryEditor or RequireCampaignAccess. A convention test checks this.
/// </summary>
internal sealed record AccessRuleMetadata(string Rule);

internal static class AccessRuleExtensions
{
    public const string AdminPolicy = "AdminOnly";

    public const string LibraryPolicy = "LibraryEditor";

    /// <summary>Any signed-in user.</summary>
    public static TBuilder RequireSignedIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization().WithMetadata(new AccessRuleMetadata("signed-in"));

    /// <summary>Admins only. Applied once to the /api/admin group.</summary>
    public static TBuilder AdminOnly<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(AdminPolicy).WithMetadata(new AccessRuleMetadata("admin"));

    /// <summary>Managers and Admins: changing the library (decision 0015).</summary>
    public static TBuilder RequireLibraryEditor<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder
            .RequireAuthorization(LibraryPolicy)
            .WithMetadata(new AccessRuleMetadata("library-editor"));

    public static AuthorizationBuilder AddAccessPolicies(this AuthorizationBuilder authorization) =>
        authorization
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(Roles.Admin))
            .AddPolicy(
                LibraryPolicy,
                policy => policy.RequireAuthenticatedUser().AddRequirements(new LibraryEditor())
            );
}

/// <summary>An Admin, or a Manager (decision 0015).</summary>
internal sealed class LibraryEditor : IAuthorizationRequirement;

/// <summary>
/// Admins pass on their role, as everywhere. The Manager role is read from the database, not the
/// token: an Admin grants and removes it on the user's admin page, and that applies at once rather
/// than at the user's next token refresh.
/// </summary>
internal sealed class LibraryEditorHandler(UserManager<AppUser> users)
    : AuthorizationHandler<LibraryEditor>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        LibraryEditor requirement
    )
    {
        if (context.User.IsInRole(Roles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        if (
            await users.GetUserAsync(context.User) is { } user
            && await users.IsInRoleAsync(user, Roles.Manager)
        )
        {
            context.Succeed(requirement);
        }
    }
}
