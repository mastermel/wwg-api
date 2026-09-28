namespace Wwg.Api.Data.Entities;

/// <summary>An army in a campaign, optionally commanded by one of its Players.</summary>
internal sealed class Army : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public required string Name { get; set; }

    /// <summary>The commanding member (a Player in the same campaign), or null if unassigned.</summary>
    public Guid? CommanderId { get; set; }

    public CampaignMember? Commander { get; set; }
}
