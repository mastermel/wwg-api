using Wwg.Api.Features.Account;
using Wwg.Api.Features.Admin;
using Wwg.Api.Features.Armies;
using Wwg.Api.Features.ArmyUnits;
using Wwg.Api.Features.Auth;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Features.Health;
using Wwg.Api.Features.Intelligence;
using Wwg.Api.Features.Join;
using Wwg.Api.Features.Library;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Sides;
using Wwg.Api.Features.Sightings;
using Wwg.Api.Features.Supply;
using Wwg.Api.Features.Turns;
using Wwg.Api.Features.Victory;

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
            .MapMemberEndpoints()
            .MapJoinEndpoints()
            .MapSideEndpoints()
            .MapArmyEndpoints()
            .MapArmyUnitEndpoints()
            .MapMapEndpoints()
            .MapGridEndpoints()
            .MapHexDetailEndpoints()
            .MapCalendarEndpoints()
            .MapConcentrationEndpoints()
            .MapTurnEndpoints()
            .MapMovementEndpoints()
            .MapOrderEndpoints()
            .MapMarchEndpoints()
            .MapDepotEndpoints()
            .MapSupplySettingsEndpoints()
            .MapSightingEndpoints()
            .MapIntelEndpoints()
            .MapVictoryEndpoints()
            .MapTurnActionEndpoints()
            .MapLibraryEndpoints();
    }
}
