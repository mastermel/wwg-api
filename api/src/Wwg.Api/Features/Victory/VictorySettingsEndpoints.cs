using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Victory;

/// <summary>A campaign's victory points settings (decision 0021).</summary>
/// <param name="Mode">Every settlement by the rules' table, or only those the Umpire gives points.</param>
public sealed record VictorySettingsResponse(VictoryPointsMode Mode);

/// <summary>The Umpire sets which settlements are worth victory points.</summary>
/// <param name="Mode">Every settlement by the rules' table, or only those the Umpire gives points.</param>
public sealed record UpdateVictorySettingsRequest(
    [property: JsonRequired, EnumDataType(typeof(VictoryPointsMode))] VictoryPointsMode Mode
);

/// <summary>Which settlements are worth victory points (step 50).</summary>
internal static class VictorySettingsEndpoints
{
    public static IEndpointRouteBuilder MapVictorySettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/campaigns/{id:guid}/victory-settings")
            .WithTags("Victory");
        settings
            .MapGet("", GetVictorySettingsAsync)
            .WithName("GetVictorySettings")
            .RequireCampaignAccess(CampaignAccess.Member);
        settings
            .MapPut("", UpdateVictorySettingsAsync)
            .WithName("UpdateVictorySettings")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// Which settlements are worth victory points (every member): every town, city and fortress by
    /// the rules' table, or only those the Umpire gives points.
    /// </summary>
    internal static async Task<Ok<VictorySettingsResponse>> GetVictorySettingsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            new VictorySettingsResponse(
                (await CalendarEndpoints.LoadAsync(db, id, cancellationToken)).VictoryPoints
            )
        );

    /// <summary>
    /// Changes which settlements are worth victory points (Umpire or Admin). Points the Umpire set
    /// stay, either way; the totals follow at once.
    /// </summary>
    internal static async Task<Ok<VictorySettingsResponse>> UpdateVictorySettingsAsync(
        Guid id,
        UpdateVictorySettingsRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        campaign.VictoryPoints = request.Mode;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new VictorySettingsResponse(campaign.VictoryPoints));
    }
}
