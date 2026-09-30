using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Campaigns;

/// <summary>A campaign's calendar (step 45): when its turns fall, and whose infantry march faster or slower.</summary>
/// <param name="StartDate">The first turn's day, or null if not set.</param>
/// <param name="FirstTurnPart">The first turn's time of day.</param>
/// <param name="MorningNations">Whose infantry move a flat hex further each Morning.</param>
/// <param name="AfternoonNations">Whose infantry move a flat hex less each Afternoon.</param>
/// <param name="Rules">Whether the nations are the rule book's.</param>
public sealed record CampaignCalendarResponse(
    DateOnly? StartDate,
    TurnPart FirstTurnPart,
    IReadOnlyList<Nation> MorningNations,
    IReadOnlyList<Nation> AfternoonNations,
    bool Rules
);

/// <summary>The Umpire sets the campaign's calendar.</summary>
/// <param name="StartDate">The first turn's day, or null for none.</param>
/// <param name="FirstTurnPart">The first turn's time of day.</param>
/// <param name="MorningNations">Whose infantry move a flat hex further each Morning.</param>
/// <param name="AfternoonNations">Whose infantry move a flat hex less each Afternoon.</param>
public sealed record UpdateCampaignCalendarRequest(
    DateOnly? StartDate,
    [property: JsonRequired, EnumDataType(typeof(TurnPart))] TurnPart FirstTurnPart,
    [property: Required, MaxLength(30)] IReadOnlyList<Nation> MorningNations,
    [property: Required, MaxLength(30)] IReadOnlyList<Nation> AfternoonNations
);
