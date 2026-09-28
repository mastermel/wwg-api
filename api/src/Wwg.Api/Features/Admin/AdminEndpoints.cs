using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Admin;

internal static class AdminEndpoints
{
    /// <summary>
    /// Everything admin-only lives under /api/admin, and AdminOnly is applied once, here: there
    /// are no admin-only query parameters or one-off admin routes elsewhere (DESIGN.md §3.5).
    /// </summary>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGroup("/api/admin").WithTags("Admin").AdminOnly().MapAdminUserEndpoints();
        return app;
    }
}
