using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Campaigns;

/// <summary>
/// A campaign's concentration settings (step 46, decision 0017): the most points of infantry and
/// of cavalry a side may have in a hex, and the unit types counted towards each (the rest are free).
/// </summary>
/// <param name="InfantryTypes">The unit types counted towards the infantry limit.</param>
/// <param name="CavalryTypes">The unit types counted towards the cavalry limit.</param>
/// <param name="InfantryLimit">The most points of infantry a side may have in a hex.</param>
/// <param name="CavalryLimit">The most points of cavalry a side may have in a hex.</param>
public sealed record CampaignConcentrationResponse(
    IReadOnlyList<UnitType> InfantryTypes,
    IReadOnlyList<UnitType> CavalryTypes,
    int InfantryLimit,
    int CavalryLimit
);

/// <summary>The Umpire sets the campaign's concentration settings.</summary>
/// <param name="InfantryTypes">The unit types counted towards the infantry limit.</param>
/// <param name="CavalryTypes">The unit types counted towards the cavalry limit (none in both).</param>
/// <param name="InfantryLimit">The most points of infantry a side may have in a hex.</param>
/// <param name="CavalryLimit">The most points of cavalry a side may have in a hex.</param>
public sealed record UpdateCampaignConcentrationRequest(
    [property: Required, MaxLength(20)] IReadOnlyList<UnitType> InfantryTypes,
    [property: Required, MaxLength(20)] IReadOnlyList<UnitType> CavalryTypes,
    [property: JsonRequired, Range(1, Concentration.MaxLimit)] int InfantryLimit,
    [property: JsonRequired, Range(1, Concentration.MaxLimit)] int CavalryLimit
);
