namespace Wwg.Api.IntegrationTests.Support;

/// <summary>The roles in DESIGN.md §5.2's permission matrix. (Commander arrives with armies.)</summary>
public enum Role
{
    Admin,
    Umpire,
    Player,
    NonMember,
}

/// <summary>
/// A campaign with an Umpire and a Player, plus an Admin and a signed-in outsider, each with their
/// own client. Permission tests pick a client by <see cref="Role"/>.
/// </summary>
internal sealed class CampaignScenario(
    Guid campaignId,
    Guid umpireMemberId,
    Guid playerMemberId,
    IReadOnlyDictionary<Role, HttpClient> clients
) : IDisposable
{
    public Guid CampaignId { get; } = campaignId;

    /// <summary>The Umpire's membership ID.</summary>
    public Guid UmpireMemberId { get; } = umpireMemberId;

    /// <summary>The Player's membership ID.</summary>
    public Guid PlayerMemberId { get; } = playerMemberId;

    public HttpClient As(Role role) => clients[role];

    public void Dispose()
    {
        foreach (var client in clients.Values)
        {
            client.Dispose();
        }
    }
}
