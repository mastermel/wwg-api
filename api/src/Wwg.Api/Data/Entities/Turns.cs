using Wwg.Api.Features.Maps;

namespace Wwg.Api.Data.Entities;

/// <summary>
/// One turn of a campaign (decision 0010): every army moves in it before the next opens. Turn 0
/// is setup, where the Umpire places every unit. The open turn is the one with no ClosedAt.
/// </summary>
internal sealed class CampaignTurn : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>0 for setup, then 1, 2…</summary>
    public int Number { get; set; }

    public DateTime OpenedAt { get; set; }

    /// <summary>When the next turn opened (or the campaign started, for turn 0); null while open.</summary>
    public DateTime? ClosedAt { get; set; }
}

/// <summary>Where an army's turn is.</summary>
public enum ArmyTurnStatus
{
    /// <summary>Its commander is giving orders (or the Umpire placing units, in turn 0).</summary>
    Draft,

    /// <summary>Sent to the Umpire; nothing can change.</summary>
    Submitted,

    /// <summary>Approved: its orders are where the army's units now are.</summary>
    Completed,
}

/// <summary>One army's part in a campaign turn: the orders it gives its units.</summary>
internal sealed class ArmyTurn : Entity
{
    public Guid CampaignTurnId { get; set; }

    public CampaignTurn CampaignTurn { get; set; } = null!; // Set by EF Core when loaded.

    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    public ArmyTurnStatus Status { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

/// <summary>What a unit does in a turn.</summary>
public enum OrderKind
{
    /// <summary>Goes to the order's position (in turn 0: is placed there).</summary>
    Move,

    /// <summary>Stays where it is (the order keeps that position, so every turn is complete).</summary>
    Hold,

    /// <summary>
    /// Boards its army's free boats in its hex, for the whole turn (decision 0022): they're tied to
    /// it from then on, and it moves by them.
    /// </summary>
    Embark,

    /// <summary>
    /// Lands from its boats, for the whole turn: in its hex, across a river side of it, or from a
    /// lake onto a shore beside it (the order's position). The boats stay, free, where it was.
    /// </summary>
    Disembark,
}

/// <summary>A unit's order in an army's turn, and so its position after that turn.</summary>
internal sealed class UnitOrder : Entity
{
    public Guid ArmyTurnId { get; set; }

    public ArmyTurn ArmyTurn { get; set; } = null!; // Set by EF Core when loaded.

    public Guid UnitId { get; set; }

    public ArmyUnit ArmyUnit { get; set; } = null!; // Set by EF Core when loaded.

    public OrderKind Kind { get; set; }

    /// <summary>The hex the order leaves the unit in (decision 0014).</summary>
    public int Q { get; set; }

    public int R { get; set; }

    /// <summary>
    /// A Move's steps, the hexes it passes through in order (the last is Q, R). Empty for a Hold, a
    /// placement, or a move from before the grid.
    /// </summary>
    public List<Hex> Path { get; set; } = [];

    /// <summary>
    /// Part of the way (0–1) into the path's last hex, when that hex takes more than a turn (step
    /// 44): the unit is still in Q, R, the hex before it. Null when it got where it was going.
    /// </summary>
    public double? Progress { get; set; }

    /// <summary>Set by the Umpire (or an Admin) on the commander's behalf (decision 0011).</summary>
    public bool ByUmpire { get; set; }

    /// <summary>
    /// A force march (decision 0018): a Move a flat hex's worth further, in a day turn. It counts
    /// towards the unit's forced marches, as moving turns in a row do.
    /// </summary>
    public bool ForceMarch { get; set; }

    /// <summary>
    /// The unit lives off the land this turn (decision 0019): it can't be out of supply, and its
    /// side's hex is held to half the concentration limits.
    /// </summary>
    public bool LivesOffTheLand { get; set; }

    /// <summary>
    /// The boats the unit is on in this turn (decision 0022): those it embarks on, moves with or
    /// lands from. Empty for a unit ashore.
    /// </summary>
    public List<Guid> Boats { get; set; } = [];

    /// <summary>
    /// For a boat tied to a unit: that unit, whose order this one follows (written with it, never
    /// given itself). Null for every other order.
    /// </summary>
    public Guid? CarrierId { get; set; }
}

/// <summary>What happened to an army's turn.</summary>
public enum ArmyTurnEventKind
{
    Submitted,
    Approved,
    SentBack,
    Reverted,

    /// <summary>The Umpire changed orders (a note per unit): one event per run of changes.</summary>
    Edited,
}

/// <summary>
/// One step in an army turn's history (DESIGN.md §5.1): who did what, when, and the Umpire's note
/// if they sent it back or reverted it, with notes on particular units.
/// </summary>
internal sealed class ArmyTurnEvent : Entity
{
    public Guid ArmyTurnId { get; set; }

    public ArmyTurn ArmyTurn { get; set; } = null!; // Set by EF Core when loaded.

    public ArmyTurnEventKind Kind { get; set; }

    public DateTime At { get; set; }

    /// <summary>Who did it; null if their account has since been deleted.</summary>
    public Guid? ByUserId { get; set; }

    public AppUser? ByUser { get; set; }

    public string? Note { get; set; }

    public List<UnitNote> UnitNotes { get; } = [];
}

/// <summary>The Umpire's note on one unit's order, when sending a turn back or reverting it.</summary>
internal sealed class UnitNote : Entity
{
    public Guid ArmyTurnEventId { get; set; }

    public Guid UnitId { get; set; }

    public ArmyUnit ArmyUnit { get; set; } = null!; // Set by EF Core when loaded.

    public required string Text { get; set; }
}
