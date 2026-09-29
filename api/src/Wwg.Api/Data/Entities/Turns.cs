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
}

/// <summary>A unit's order in an army's turn, and so its position after that turn.</summary>
internal sealed class UnitOrder : Entity
{
    public Guid ArmyTurnId { get; set; }

    public ArmyTurn ArmyTurn { get; set; } = null!; // Set by EF Core when loaded.

    public Guid UnitId { get; set; }

    public Unit Unit { get; set; } = null!; // Set by EF Core when loaded.

    public OrderKind Kind { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }
}
