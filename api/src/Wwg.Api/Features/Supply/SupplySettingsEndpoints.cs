using Microsoft.AspNetCore.Http.HttpResults;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Campaigns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Supply;

/// <summary>A campaign's supply settings (step 48b): reach, exempt types, living off the land.</summary>
internal static class SupplySettingsEndpoints
{
    public static IEndpointRouteBuilder MapSupplySettingsEndpoints(this IEndpointRouteBuilder app)
    {
        var settings = app.MapGroup("/api/campaigns/{id:guid}/supply-settings").WithTags("Supply");
        settings
            .MapGet("", GetSupplySettingsAsync)
            .WithName("GetSupplySettings")
            .RequireCampaignAccess(CampaignAccess.Member);
        settings
            .MapPut("", UpdateSupplySettingsAsync)
            .WithName("UpdateSupplySettings")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// The campaign's supply settings (every member): how far from its army's routes a unit may
    /// be, the unit types that don't need supply, and the nations that may live off the land.
    /// </summary>
    internal static async Task<Ok<CampaignSupplySettingsResponse>> GetSupplySettingsAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(ToResponse(await CalendarEndpoints.LoadAsync(db, id, cancellationToken)));

    /// <summary>Changes the campaign's supply settings (Umpire or Admin), from the next turn's close.</summary>
    internal static async Task<
        Results<Ok<CampaignSupplySettingsResponse>, ValidationProblem>
    > UpdateSupplySettingsAsync(
        Guid id,
        UpdateCampaignSupplySettingsRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (request.ExemptTypes.Any(t => !Enum.IsDefined(t)))
        {
            return Invalid("exemptTypes", "Choose unit types from the list.");
        }
        if (request.OffTheLandNations.Any(n => !Enum.IsDefined(n) || n == Nation.None))
        {
            return Invalid("offTheLandNations", "Choose nations from the list.");
        }

        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        campaign.SupplyReach = request.Reach;
        // The usual lists are kept as "the usual", so they follow any change to them.
        campaign.SupplyExemptTypes = SameAs(request.ExemptTypes, SupplyRules.ExemptTypes)
            ? null
            : [.. request.ExemptTypes.Distinct()];
        campaign.OffTheLandNations = SameAs(
            request.OffTheLandNations,
            SupplyRules.OffTheLandNations
        )
            ? null
            : [.. request.OffTheLandNations.Distinct()];
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(campaign));
    }

    /// <summary>The nations whose units may live off the land, in this campaign.</summary>
    public static IReadOnlyList<Nation> OffTheLandNations(Campaign campaign) =>
        campaign.OffTheLandNations ?? SupplyRules.OffTheLandNations;

    /// <summary>The unit types that don't need supply, in this campaign.</summary>
    public static IReadOnlyList<UnitType> ExemptTypes(Campaign campaign) =>
        campaign.SupplyExemptTypes ?? SupplyRules.ExemptTypes;

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }
        );

    private static bool SameAs<T>(IReadOnlyList<T> given, IReadOnlyList<T> usual) =>
        given.ToHashSet().SetEquals(usual);

    private static CampaignSupplySettingsResponse ToResponse(Campaign campaign) =>
        new(campaign.SupplyReach, [.. ExemptTypes(campaign)], [.. OffTheLandNations(campaign)]);
}
