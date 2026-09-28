using Wwg.Api.Features.Account;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Auth;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Health;

namespace Wwg.Api.Features;

internal static class EndpointRouteBuilderExtensions
{
    /// <summary>Maps every feature's endpoints. Each feature adds one <c>Map{Feature}Endpoints</c> call here.</summary>
    public static IEndpointRouteBuilder MapApiEndpoints(this IEndpointRouteBuilder app)
    {
        return app.MapHealthEndpoints()
            .MapAuthEndpoints()
            .MapAccountEndpoints()
            .MapAdminEndpoints()
            .MapCampaignEndpoints()
            .MapMemberEndpoints();
    }
}
