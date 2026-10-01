namespace Wwg.Api.Data.Entities;

/// <summary>Why a unit's points changed (decision 0018).</summary>
public enum PointsChangeReason
{
    /// <summary>Lost to attrition, as the Umpire confirmed when the next turn started.</summary>
    Attrition,

    /// <summary>The Umpire changed them on the unit.</summary>
    Edited,
}

/// <summary>
/// One change to a unit's points once the campaign has started (decision 0018): its turn, by how
/// much, what they came to, and why. Every member sees them, as they see the points.
/// </summary>
internal sealed class PointsChange : Entity
{
    public Guid ArmyUnitId { get; set; }

    public ArmyUnit ArmyUnit { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The turn it belongs to: the one attrition closed, or the one open at an edit.</summary>
    public int Turn { get; set; }

    /// <summary>Points gained (or, below 0, lost).</summary>
    public int Change { get; set; }

    public int PointsAfter { get; set; }

    public PointsChangeReason Reason { get; set; }

    /// <summary>What it was for ("Forced march, ×2"), if anything.</summary>
    public string? Note { get; set; }

    /// <summary>Who made it; null if their account has since been deleted.</summary>
    public Guid? ByUserId { get; set; }

    public AppUser? ByUser { get; set; }
}
