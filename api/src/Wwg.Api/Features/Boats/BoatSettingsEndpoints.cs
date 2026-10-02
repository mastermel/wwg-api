using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Boats;

/// <summary>A campaign's boat settings (decision 0022).</summary>
/// <param name="Capacity">The points a boat carries: a unit needs a boat for each, or part.</param>
public sealed record BoatSettingsResponse(int Capacity);

/// <summary>The Umpire sets what a boat carries.</summary>
/// <param name="Capacity">The points a boat carries, 1–100 (Chart #11: 14).</param>
public sealed record UpdateBoatSettingsRequest(
    [property: JsonRequired, Range(BoatRules.MinCapacity, BoatRules.MaxCapacity)] int Capacity
);

/// <summary>What a boat carries (step 51).</summary>
internal static class BoatSettingsEndpoints
{
    public static IEndpointRouteBuilder MapBoatSettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/campaigns/{id:guid}/boat-settings").WithTags("Boats");
        settings
            .MapGet("", GetBoatSettingsAsync)
            .WithName("GetBoatSettings")
            .RequireCampaignAccess(CampaignAccess.Member);
        settings
            .MapPut("", UpdateBoatSettingsAsync)
            .WithName("UpdateBoatSettings")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>What a boat carries in the campaign (every member), in points.</summary>
    internal static async Task<Ok<BoatSettingsResponse>> GetBoatSettingsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) =>
        TypedResults.Ok(
            new BoatSettingsResponse(
                (await CalendarEndpoints.LoadAsync(db, id, cancellationToken)).BoatCapacity
            )
        );

    /// <summary>
    /// Changes what a boat carries (Umpire or Admin), for units embarking from now on: those on
    /// boats keep theirs.
    /// </summary>
    internal static async Task<Ok<BoatSettingsResponse>> UpdateBoatSettingsAsync(
        Guid id,
        UpdateBoatSettingsRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        campaign.BoatCapacity = request.Capacity;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(new BoatSettingsResponse(campaign.BoatCapacity));
    }
}
