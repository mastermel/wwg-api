namespace Wwg.Api.Data.Entities;

/// <summary>
/// Who holds a settlement (decision 0021): an army, or no one. The Umpire sets who starts with it;
/// as each turn closes, an army alone in its hex takes it.
/// </summary>
internal sealed class Holding : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The settlement's hex.</summary>
    public int Q { get; set; }

    public int R { get; set; }

    /// <summary>Its holder, or null for no one.</summary>
    public Guid? ArmyId { get; set; }

    public Army? Army { get; set; }
}

/// <summary>A settlement changing hands (decision 0021), kept for the scoreboard's history.</summary>
internal sealed class HoldingChange : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The turn it happened in (0: set up by the Umpire before the start).</summary>
    public int Turn { get; set; }

    public int Q { get; set; }

    public int R { get; set; }

    /// <summary>Who held it before, if anyone (null too if their army has since gone).</summary>
    public Guid? FromArmyId { get; set; }

    public Army? FromArmy { get; set; }

    /// <summary>Who holds it after, if anyone.</summary>
    public Guid? ToArmyId { get; set; }

    public Army? ToArmy { get; set; }

    /// <summary>Set by the Umpire rather than taken.</summary>
    public bool ByUmpire { get; set; }
}
