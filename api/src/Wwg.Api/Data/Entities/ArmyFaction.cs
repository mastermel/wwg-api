namespace Wwg.Api.Data.Entities;

/// <summary>
/// A library faction an army takes its units from (decision 0015). An army can choose several
/// (allied contingents); one can't be dropped while the army has units from it.
/// </summary>
internal sealed class ArmyFaction : Entity
{
    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    public Guid FactionId { get; set; }

    public Faction Faction { get; set; } = null!; // Set by EF Core when loaded.
}
