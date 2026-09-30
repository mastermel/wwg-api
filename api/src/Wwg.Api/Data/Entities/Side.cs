namespace Wwg.Api.Data.Entities;

/// <summary>A side in a campaign. Each army belongs to one (or none yet: "Unassigned").</summary>
internal sealed class Side : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }
}
