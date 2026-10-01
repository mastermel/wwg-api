using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Supply;

/// <summary>A campaign's supply settings (step 48, decision 0019).</summary>
/// <param name="Reach">How far from its army's supply routes a unit may be, in hexes.</param>
/// <param name="ExemptTypes">The unit types that don't need supply.</param>
/// <param name="OffTheLandNations">The nations whose units may live off the land.</param>
public sealed record CampaignSupplySettingsResponse(
    int Reach,
    IReadOnlyList<UnitType> ExemptTypes,
    IReadOnlyList<Nation> OffTheLandNations
);

/// <summary>The Umpire sets the campaign's supply settings.</summary>
/// <param name="Reach">How far from its army's supply routes a unit may be, 0 to 3 hexes.</param>
/// <param name="ExemptTypes">The unit types that don't need supply.</param>
/// <param name="OffTheLandNations">The nations whose units may live off the land.</param>
public sealed record UpdateCampaignSupplySettingsRequest(
    [property: JsonRequired, Range(0, SupplyRules.MaxReach)] int Reach,
    [property: Required, MaxLength(20)] IReadOnlyList<UnitType> ExemptTypes,
    [property: Required, MaxLength(30)] IReadOnlyList<Nation> OffTheLandNations
);
