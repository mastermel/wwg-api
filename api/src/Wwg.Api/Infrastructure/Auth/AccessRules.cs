using Microsoft.AspNetCore.Authorization;

namespace Wwg.Api.Infrastructure.Auth;

internal static class Roles
{
    /// <summary>The site-wide Admin role (DESIGN.md §3.5), synced from Admin:Emails.</summary>
    public const string Admin = "Admin";
}

/// <summary>
/// Marks an endpoint's declared access rule. Sign-in is required by default (a fallback policy),
/// but every endpoint must still say which rule it means: AllowAnonymous, RequireSignedIn,
/// AdminOnly or (later) RequireCampaignAccess. A convention test checks this.
/// </summary>
internal sealed record AccessRuleMetadata(string Rule);

internal static class AccessRuleExtensions
{
    public const string AdminPolicy = "AdminOnly";

    /// <summary>Any signed-in user.</summary>
    public static TBuilder RequireSignedIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization().WithMetadata(new AccessRuleMetadata("signed-in"));

    /// <summary>Admins only. Applied once to the /api/admin group.</summary>
    public static TBuilder AdminOnly<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(AdminPolicy).WithMetadata(new AccessRuleMetadata("admin"));

    public static AuthorizationBuilder AddAccessPolicies(this AuthorizationBuilder authorization) =>
        authorization
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(AdminPolicy, policy => policy.RequireRole(Roles.Admin));
}
