namespace Wwg.Api.Data.Entities;

/// <summary>A campaign role (stored as its name).</summary>
public enum CampaignRole
{
    Umpire,
    Player,
}

/// <summary>A user's place in a campaign. <see cref="Entity.CreatedAt"/> is when they joined.</summary>
internal sealed class CampaignMember : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!; // Set by EF Core when loaded.

    public CampaignRole Role { get; set; }
}
