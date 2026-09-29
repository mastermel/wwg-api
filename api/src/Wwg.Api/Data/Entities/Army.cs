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

    /// <summary>The army's side, or null until the Umpire assigns one ("Unassigned").</summary>
    public Guid? FactionId { get; set; }

    public Faction? Faction { get; set; }

    /// <summary>At most this many armies in a campaign: one per palette colour.</summary>
    public const int MaxPerCampaign = 8;

    public ArmyColor Color { get; set; }

    public Nation Nation { get; set; }
}
