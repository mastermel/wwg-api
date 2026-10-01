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
