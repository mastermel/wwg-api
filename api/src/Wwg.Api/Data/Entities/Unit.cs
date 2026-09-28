namespace Wwg.Api.Data.Entities;

/// <summary>A unit in an army. Only the army's commander, the Umpire and Admins see it.</summary>
internal sealed class Unit : Entity
{
    public Guid ArmyId { get; set; }

    public Army Army { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }
}
