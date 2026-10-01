using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Intelligence;

/// <summary>A commander's report to an ally (decision 0020), carried by courier.</summary>
/// <param name="ToArmyId">The ally: another army of the same side.</param>
/// <param name="IncludesSnapshot">Whether it carries the army's units: where they are, and their points.</param>
/// <param name="IncludesSightings">Whether it carries every sighting the army has received so far.</param>
/// <param name="Note">A note, if any.</param>
public sealed record SendReportRequest(
    [property: JsonRequired] Guid ToArmyId,
    [property: JsonRequired] bool IncludesSnapshot,
    [property: JsonRequired] bool IncludesSightings,
    [property: Trimmed, StringLength(1000)] string? Note
);

/// <summary>A unit as a report's snapshot has it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Q">Its hex (axial q).</param>
/// <param name="R">Its hex (axial r).</param>
/// <param name="Latitude">The hex's centre.</param>
/// <param name="Longitude">The hex's centre.</param>
/// <param name="Points">Its points.</param>
public sealed record SnapshotUnitResponse(
    string Name,
    UnitType Type,
    int Q,
    int R,
    double Latitude,
    double Longitude,
    int Points
);

/// <summary>A report between allies, as its sender, its recipient or the Umpire sees it.</summary>
/// <param name="Id">The report.</param>
/// <param name="FromArmyId">The sender's army.</param>
/// <param name="ToArmyId">The recipient's army.</param>
/// <param name="SentTurn">The turn it was sent in.</param>
/// <param name="ArrivedTurn">The turn it arrived for, once it has.</param>
/// <param name="Status">On its way, arrived, or stopped (a sender isn't told which).</param>
/// <param name="Note">Its note, if any.</param>
/// <param name="Snapshot">The sender's units as they were, if sent.</param>
/// <param name="SightingCount">How many sightings it carries.</param>
public sealed record ReportResponse(
    Guid Id,
    Guid FromArmyId,
    Guid ToArmyId,
    int SentTurn,
    int? ArrivedTurn,
    CourierStatus? Status,
    string? Note,
    IReadOnlyList<SnapshotUnitResponse>? Snapshot,
    int SightingCount
);

/// <summary>A courier on its way, for the Umpire (decision 0020).</summary>
/// <param name="ReportId">The report it carries.</param>
/// <param name="FromArmyId">The sender's army.</param>
/// <param name="ToArmyId">The recipient's army.</param>
/// <param name="SentTurn">The turn it was sent in.</param>
/// <param name="Q">Its hex (axial q).</param>
/// <param name="R">Its hex (axial r).</param>
/// <param name="Latitude">The hex's centre.</param>
/// <param name="Longitude">The hex's centre.</param>
/// <param name="ArrivesNext">It arrives as the next turn starts.</param>
/// <param name="AmongTheEnemy">Units of the other side are in its hex: the Umpire's to stop or let through.</param>
public sealed record CourierResponse(
    Guid ReportId,
    Guid FromArmyId,
    Guid ToArmyId,
    int SentTurn,
    int Q,
    int R,
    double Latitude,
    double Longitude,
    bool ArrivesNext,
    bool AmongTheEnemy
);
