using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Supply;

/// <summary>One of an army's depots (decision 0019).</summary>
/// <param name="Id">The depot's ID.</param>
/// <param name="ArmyId">Its army: only its units draw supply from it.</param>
/// <param name="Kind">Main, or intermediate (supplied from a main one, stocked for 15 turns).</param>
/// <param name="Name">Its name, if given.</param>
/// <param name="Q">The hex it's in (axial q).</param>
/// <param name="R">The hex it's in (axial r).</param>
/// <param name="Latitude">The hex's centre, in degrees.</param>
/// <param name="Longitude">The hex's centre, in degrees.</param>
/// <param name="CutOffTurns">An intermediate depot's turns in a row cut off from a main one.</param>
public sealed record DepotResponse(
    Guid Id,
    Guid ArmyId,
    DepotKind Kind,
    string? Name,
    int Q,
    int R,
    double Latitude,
    double Longitude,
    int CutOffTurns
);

/// <summary>The Umpire places, moves or changes a depot.</summary>
/// <param name="Kind">Main or intermediate.</param>
/// <param name="Name">Its name, if any.</param>
/// <param name="Q">The hex (axial q), inside the campaign's grid.</param>
/// <param name="R">The hex (axial r).</param>
public sealed record SaveDepotRequest(
    [property: JsonRequired, EnumDataType(typeof(DepotKind))] DepotKind Kind,
    [property: Trimmed, StringLength(100)] string? Name,
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R
);

/// <summary>A unit's supply (decision 0019): as the open turn began, and by its orders as given.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="ArmyId">Its army.</param>
/// <param name="State">Its supply as the open turn began.</param>
/// <param name="DepotId">The depot it's supplied from, if it is.</param>
/// <param name="UnsuppliedTurns">Turns in a row it has ended out of supply.</param>
/// <param name="NextState">Its supply where its order this turn (as given) leaves it.</param>
/// <param name="NextUnsuppliedTurns">Its turns in a row out of supply, after this one.</param>
public sealed record UnitSupplyResponse(
    Guid UnitId,
    Guid ArmyId,
    SupplyState State,
    Guid? DepotId,
    int UnsuppliedTurns,
    SupplyState NextState,
    int NextUnsuppliedTurns
);

/// <summary>An intermediate depot's supply (decision 0019).</summary>
/// <param name="DepotId">The depot.</param>
/// <param name="Connected">Whether a route reaches it from a main depot of its army now.</param>
/// <param name="CutOffTurns">Turns in a row it has been cut off (its stock lasts 15).</param>
public sealed record DepotSupplyResponse(Guid DepotId, bool Connected, int CutOffTurns);

/// <summary>The supply the viewer may see: their army's (the Umpire's, every army's).</summary>
/// <param name="Units">Each unit's supply.</param>
/// <param name="Depots">Each intermediate depot's.</param>
public sealed record CampaignSupplyResponse(
    IReadOnlyList<UnitSupplyResponse> Units,
    IReadOnlyList<DepotSupplyResponse> Depots
);
