using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;

namespace Wwg.Api.Features.Campaigns;

/// <summary>A campaign's calendar (step 45): its turns' days and times of day.</summary>
internal static class CalendarEndpoints
{
    public static IEndpointRouteBuilder MapCalendarEndpoints(this IEndpointRouteBuilder app)
    {
        var calendar = app.MapGroup("/api/campaigns/{id:guid}/calendar").WithTags("Campaigns");
        calendar
            .MapGet("", GetCampaignCalendarAsync)
            .WithName("GetCampaignCalendar")
            .RequireCampaignAccess(CampaignAccess.Member);
        calendar
            .MapPut("", UpdateCampaignCalendarAsync)
            .WithName("UpdateCampaignCalendar")
            .RequireCampaignAccess(CampaignAccess.Umpire);
        return app;
    }

    /// <summary>
    /// The campaign's calendar (every member): the first turn's day and time of day, and whose
    /// infantry march further each Morning or less each Afternoon (the rule book's, until the
    /// Umpire changes them).
    /// </summary>
    internal static async Task<Ok<CampaignCalendarResponse>> GetCampaignCalendarAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(ToResponse(await LoadAsync(db, id, cancellationToken)));

    /// <summary>
    /// Changes the campaign's calendar (Umpire or Admin). Its turns are relabelled, those played
    /// too; the marches apply to orders given from now on.
    /// </summary>
    internal static async Task<
        Results<Ok<CampaignCalendarResponse>, ValidationProblem>
    > UpdateCampaignCalendarAsync(
        Guid id,
        UpdateCampaignCalendarRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (
            request.MorningNations.Concat(request.AfternoonNations).Any(n => !Enum.IsDefined(n))
            || request.MorningNations.Contains(Nation.None)
            || request.AfternoonNations.Contains(Nation.None)
        )
        {
            return TypedResults.ValidationProblem(
                new Dictionary<string, string[]>(StringComparer.Ordinal)
                {
                    ["morningNations"] = ["Choose nations from the list."],
                }
            );
        }

        var campaign = await db
            .Campaigns.Where(c => c.Id == id)
            .SingleOrGoneAsync(cancellationToken);
        campaign.StartDate = request.StartDate;
        campaign.FirstTurnPart = request.FirstTurnPart;
        // The rules' own lists are kept as "the rules'", so they follow any change to them.
        campaign.MorningNations = SameAs(request.MorningNations, TurnParts.MorningNations)
            ? null
            : [.. request.MorningNations.Distinct()];
        campaign.AfternoonNations = SameAs(request.AfternoonNations, TurnParts.AfternoonNations)
            ? null
            : [.. request.AfternoonNations.Distinct()];
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(ToResponse(campaign));
    }

    /// <summary>The campaign's calendar, as marches and turn labels use it.</summary>
    public static Task<Campaign> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    ) =>
        db
            .Campaigns.AsNoTracking()
            .Where(c => c.Id == campaignId)
            .SingleOrGoneAsync(cancellationToken);

    private static bool SameAs(IReadOnlyList<Nation> given, IReadOnlyList<Nation> rules) =>
        given.ToHashSet().SetEquals(rules);

    private static CampaignCalendarResponse ToResponse(Campaign campaign) =>
        new(
            campaign.StartDate,
            campaign.FirstTurnPart,
            campaign.MorningNations ?? [.. TurnParts.MorningNations],
            campaign.AfternoonNations ?? [.. TurnParts.AfternoonNations],
            campaign.MorningNations is null && campaign.AfternoonNations is null
        );
}
