using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Turns;

/// <summary>An army's part in a campaign turn.</summary>
/// <param name="ArmyId">The army.</param>
/// <param name="Status">Draft, Submitted or Completed.</param>
/// <param name="SubmittedAt">When it was last submitted (UTC).</param>
/// <param name="CompletedAt">When it was approved (UTC).</param>
public sealed record ArmyTurnSummary(
    Guid ArmyId,
    ArmyTurnStatus Status,
    DateTime? SubmittedAt,
    DateTime? CompletedAt
);

/// <summary>One of the campaign's turns.</summary>
/// <param name="Number">0 for setup, then 1, 2…</param>
/// <param name="OpenedAt">When it opened (UTC).</param>
/// <param name="ClosedAt">When the next opened (UTC), or null while it's the open turn.</param>
/// <param name="Submitted">How many armies have submitted (or completed) it.</param>
/// <param name="Armies">How many armies take part in it.</param>
/// <param name="ArmyTurns">Each army's part: all of them for the Umpire, a commander's own for them.</param>
/// <param name="Part">Its time of day (step 45); null for turn 0, the setup.</param>
/// <param name="Date">Its day, or null for the setup or a campaign without a start date.</param>
public sealed record CampaignTurnSummary(
    int Number,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    int Submitted,
    int Armies,
    IReadOnlyList<ArmyTurnSummary> ArmyTurns,
    TurnPart? Part = null,
    DateOnly? Date = null
);

/// <summary>Where the campaign's turns are.</summary>
/// <param name="Stage">Setting up (turn 0) or running.</param>
/// <param name="OpenTurn">The open turn's number (0 while setting up).</param>
/// <param name="Turns">Every turn so far, oldest first.</param>
/// <param name="StartProblems">For the Umpire: what stops the campaign starting (while setting up) or the next turn (once running).</param>
public sealed record CampaignTurnsResponse(
    CampaignStage Stage,
    int OpenTurn,
    IReadOnlyList<CampaignTurnSummary> Turns,
    IReadOnlyList<string> StartProblems
);

/// <summary>Where a unit is (or is ordered to be) after a turn.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="ArmyId">Its army.</param>
/// <param name="Turn">The turn this is its position after.</param>
/// <param name="Status">That army turn's status (a Draft's positions are orders not yet approved).</param>
/// <param name="Kind">Its order: Move (or placed, in turn 0) or Hold.</param>
/// <param name="Q">The hex it's in (axial q).</param>
/// <param name="R">The hex it's in (axial r).</param>
/// <param name="Latitude">The hex's centre, in degrees.</param>
/// <param name="Longitude">The hex's centre, in degrees.</param>
/// <param name="Path">A Move's steps, the hexes it passes through in order; empty otherwise.</param>
/// <param name="ByUmpire">Whether the Umpire set this order, on the commander's behalf.</param>
/// <param name="Progress">
/// Part of the way (0–1) into the path's last hex, which takes more than a turn: the unit is still
/// in Q, R. Null when it got where it was going.
/// </param>
/// <param name="ForceMarch">Whether the move is a force march (decision 0018).</param>
/// <param name="LivesOffTheLand">Whether the unit lives off the land this turn (decision 0019).</param>
public sealed record UnitPosition(
    Guid UnitId,
    Guid ArmyId,
    int Turn,
    ArmyTurnStatus Status,
    OrderKind Kind,
    int Q,
    int R,
    double Latitude,
    double Longitude,
    IReadOnlyList<Hex> Path,
    bool ByUmpire,
    double? Progress,
    bool ForceMarch,
    bool LivesOffTheLand
);

/// <summary>Where the Umpire places a unit: a hex in the campaign's grid.</summary>
/// <param name="Q">The hex (axial q).</param>
/// <param name="R">The hex (axial r).</param>
public sealed record PlaceUnitRequest(
    [property: JsonRequired] int Q,
    [property: JsonRequired] int R
);

/// <summary>A unit's order for the turn.</summary>
/// <param name="Kind">Move (along the path) or Hold (stay where it is).</param>
/// <param name="Path">
/// A Move's steps: the hexes it passes through in order, from next to the unit's hex to where it
/// ends. Adjacent, inside the grid, and within what the unit can move in a turn.
/// </param>
/// <param name="ForceMarch">
/// A Move by force march (decision 0018): a flat hex's worth further, in a Morning or Afternoon
/// turn only.
/// </param>
/// <param name="LivesOffTheLand">
/// The unit lives off the land this turn (decision 0019), if its nation may: never out of supply,
/// and its side's hex held to half the concentration limits.
/// </param>
public sealed record GiveOrderRequest(
    [property: JsonRequired, EnumDataType(typeof(OrderKind))] OrderKind Kind,
    [property: MaxLength(Movement.MaxSteps)] IReadOnlyList<Hex>? Path,
    bool ForceMarch = false,
    bool LivesOffTheLand = false
);

/// <summary>The Umpire's note on one unit's order.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Text">The note.</param>
public sealed record UnitNoteDto(
    Guid UnitId,
    [property: Trimmed, Required, StringLength(1000)] string Text
);

/// <summary>One step in an army turn's history.</summary>
/// <param name="Kind">Submitted, Approved, SentBack or Reverted.</param>
/// <param name="At">When (UTC).</param>
/// <param name="ByName">Who, or null if their account is gone.</param>
/// <param name="Note">The Umpire's note, when sending back or reverting.</param>
/// <param name="UnitNotes">Notes on particular units' orders.</param>
public sealed record ArmyTurnEventDto(
    ArmyTurnEventKind Kind,
    DateTime At,
    string? ByName,
    string? Note,
    IReadOnlyList<UnitNoteDto> UnitNotes
);

/// <summary>An army's turn, with its orders and history.</summary>
/// <param name="Id">The army turn's ID (orders are given through it).</param>
/// <param name="Turn">The campaign turn's number.</param>
/// <param name="Open">Whether it's in the open campaign turn.</param>
/// <param name="Status">Draft, Submitted or Completed.</param>
/// <param name="SubmittedAt">When it was last submitted (UTC).</param>
/// <param name="CompletedAt">When it was approved (UTC).</param>
/// <param name="Orders">Its units' orders, and so positions after it.</param>
/// <param name="History">What happened to it, oldest first.</param>
public sealed record ArmyTurnDetails(
    Guid Id,
    int Turn,
    bool Open,
    ArmyTurnStatus Status,
    DateTime? SubmittedAt,
    DateTime? CompletedAt,
    IReadOnlyList<UnitPosition> Orders,
    IReadOnlyList<ArmyTurnEventDto> History
);

/// <summary>Why the Umpire sends a turn back or reverts it: a note, and notes on units.</summary>
/// <param name="Note">A note on the turn as a whole.</param>
/// <param name="UnitNotes">Notes on units in the army, at most one each.</param>
public sealed record ReviewTurnRequest(
    [property: Trimmed, StringLength(2000)] string? Note,
    [property: MaxLength(100)] IReadOnlyList<UnitNoteDto>? UnitNotes
);

/// <summary>A unit's forced marches as the open turn began (decision 0018).</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="MovesInRow">Moving turns in its run, before its first forced march is made.</param>
/// <param name="ForceMarchesInRow">Of those, force-march orders.</param>
/// <param name="ForcedMarchTurns">Turns of forced march it has still to rest off (a Hold each).</param>
/// <param name="MoveCosts">
/// The attrition moving this turn would cost, as a multiple of the rules' scale (0: none).
/// </param>
/// <param name="ForceMarchCosts">The same for a force march.</param>
/// <param name="OrderCosts">The same for its order this turn as given (0 with none: a rest).</param>
public sealed record UnitMarchResponse(
    Guid UnitId,
    int MovesInRow,
    int ForceMarchesInRow,
    int ForcedMarchTurns,
    int MoveCosts,
    int ForceMarchCosts,
    int OrderCosts
);

/// <summary>A unit's attrition for the open turn's orders, for the Umpire to confirm (decision 0018).</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="ArmyId">Its army.</param>
/// <param name="Name">Its name.</param>
/// <param name="FightingFactor">Its Fighting Factor, which sets the rules' scale.</param>
/// <param name="Points">Its points now.</param>
/// <param name="ForcedMarchTurns">Its turns of forced march, this one included.</param>
/// <param name="ForcedMarchMultiplier">
/// Its forced march's multiple of the scale (1, 2, 4…; doubled out of supply), or 0.
/// </param>
/// <param name="UnsuppliedTurns">Its turns in a row out of supply, this one included, or 0.</param>
/// <param name="Multiplier">
/// This turn's whole multiple of the scale: the forced march's, and 1 more from the 7th turn out of
/// supply.
/// </param>
/// <param name="Loss">The whole points it loses (what it carries of a point adds in).</param>
public sealed record AttritionDueResponse(
    Guid UnitId,
    Guid ArmyId,
    string Name,
    int FightingFactor,
    int Points,
    int ForcedMarchTurns,
    int ForcedMarchMultiplier,
    int UnsuppliedTurns,
    int Multiplier,
    int Loss
);

/// <summary>The Umpire's confirmed attrition for one unit.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="Points">The points it loses (0 for none).</param>
public sealed record AttritionLossRequest(
    [property: JsonRequired] Guid UnitId,
    [property: JsonRequired, Range(0, UnitStats.MaxPoints)] int Points
);

/// <summary>Starting the next turn.</summary>
/// <param name="Attrition">
/// The attrition the closing turn cost, as the Umpire confirmed it: a loss for each unit that owes
/// one (see the attrition list), and no other.
/// </param>
/// <param name="Sightings">
/// The sightings for the turn starting, as the Umpire shaped them (see the sightings due), and any
/// they added; those left out aren't seen (decision 0020).
/// </param>
public sealed record StartNextTurnRequest(
    [property: MaxLength(1000)] IReadOnlyList<AttritionLossRequest>? Attrition,
    [property: MaxLength(1000)] IReadOnlyList<Sightings.SightingRequest>? Sightings = null
);
