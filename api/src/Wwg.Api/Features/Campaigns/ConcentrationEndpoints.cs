using Microsoft.AspNetCore.Http.HttpResults;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Campaigns;

/// <summary>A campaign's concentration settings (step 46): its limits, and which types count.</summary>
internal static class ConcentrationEndpoints
{
    public static IEndpointRouteBuilder MapConcentrationEndpoints(this IEndpointRouteBuilder app)
    {
        var concentration = app.MapGroup("/api/campaigns/{id:guid}/concentration")
            .WithTags("Campaigns");
        concentration
            .MapGet("", GetCampaignConcentrationAsync)
            .WithName("GetCampaignConcentration")
            .RequireCampaignAccess(CampaignAccess.Member);
        concentration
            .MapPut("", UpdateCampaignConcentrationAsync)
            .WithName("UpdateCampaignConcentration")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// The campaign's concentration settings (every member): the most points of infantry and of
    /// cavalry a side may have in a hex, and the unit types counted towards each.
    /// </summary>
    internal static async Task<Ok<CampaignConcentrationResponse>> GetCampaignConcentrationAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(ToResponse(await CalendarEndpoints.LoadAsync(db, id, cancellationToken)));

    /// <summary>
    /// Changes the campaign's concentration settings (Umpire or Admin). A unit type counts towards
    /// one limit at most; types in neither are free.
    /// </summary>
    internal static async Task<
        Results<Ok<CampaignConcentrationResponse>, ValidationProblem>
    > UpdateCampaignConcentrationAsync(
        Guid id,
        UpdateCampaignConcentrationRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (request.InfantryTypes.Concat(request.CavalryTypes).Any(t => !Enum.IsDefined(t)))
        {
            return Invalid("infantryTypes", "Choose unit types from the list.");
        }
        if (request.CavalryTypes.Intersect(request.InfantryTypes).Any())
        {
            return Invalid("cavalryTypes", "A unit type can count towards one limit only.");
        }

        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        // The usual lists are kept as "the usual", so they follow any change to them.
        campaign.InfantryLimitTypes = SameAs(request.InfantryTypes, Concentration.InfantryTypes)
            ? null
            : [.. request.InfantryTypes.Distinct()];
        campaign.CavalryLimitTypes = SameAs(request.CavalryTypes, Concentration.CavalryTypes)
            ? null
            : [.. request.CavalryTypes.Distinct()];
        campaign.InfantryLimit = request.InfantryLimit;
        campaign.CavalryLimit = request.CavalryLimit;
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(campaign));
    }

    private static ValidationProblem Invalid(string field, string message) =>
        TypedResults.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] }
        );

    private static bool SameAs(IReadOnlyList<UnitType> given, IReadOnlyList<UnitType> usual) =>
        given.ToHashSet().SetEquals(usual);

    private static CampaignConcentrationResponse ToResponse(Campaign campaign) =>
        new(
            campaign.InfantryLimitTypes ?? [.. Concentration.InfantryTypes],
            campaign.CavalryLimitTypes ?? [.. Concentration.CavalryTypes],
            campaign.InfantryLimit,
            campaign.CavalryLimit
        );
}
