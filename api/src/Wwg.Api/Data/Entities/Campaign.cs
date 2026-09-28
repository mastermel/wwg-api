namespace Wwg.Api.Data.Entities;

internal sealed class Campaign : Entity
{
    public required string Name { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// The secret in the campaign's join link: 128 random bits, base64url (22 characters). The
    /// Umpire can regenerate it, which makes the old link stop working.
    /// </summary>
    public required string JoinCode { get; set; }

    public List<CampaignMember> Members { get; } = [];
}
