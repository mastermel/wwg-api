namespace Wwg.Api.IntegrationTests.Support;

/// <summary>The roles in DESIGN.md §5.2's permission matrix.</summary>
public enum Role
{
    Admin,
    Umpire,

    /// <summary>A Player who commands the scenario's army.</summary>
    Commander,

    /// <summary>A Player who commands no army.</summary>
    Player,
    NonMember,
}

/// <summary>
/// A campaign with an Umpire, two Players (one commanding the army "First Corps", in the side
/// "Coalition", which has the unit "1st Division"), an Admin and a
/// signed-in outsider, each with their own client. Permission tests pick a client by <see cref="Role"/>.
/// </summary>
internal sealed class CampaignScenario(
    Guid campaignId,
    Guid umpireMemberId,
    Guid commanderMemberId,
    Guid playerMemberId,
    Guid sideId,
    Guid armyId,
    Guid unitId,
    IReadOnlyDictionary<Role, HttpClient> clients
) : IDisposable
{
    public Guid CampaignId { get; } = campaignId;

    /// <summary>The Umpire's membership ID.</summary>
    public Guid UmpireMemberId { get; } = umpireMemberId;

    /// <summary>The commanding Player's membership ID.</summary>
    public Guid CommanderMemberId { get; } = commanderMemberId;

    /// <summary>The other Player's membership ID (they command nothing).</summary>
    public Guid PlayerMemberId { get; } = playerMemberId;

    /// <summary>The side "Coalition", which <see cref="ArmyId"/> is in.</summary>
    public Guid SideId { get; } = sideId;

    /// <summary>The army "First Corps", commanded by the <see cref="Role.Commander"/>.</summary>
    public Guid ArmyId { get; } = armyId;

    /// <summary>The unit "1st Division", in <see cref="ArmyId"/>.</summary>
    public Guid UnitId { get; } = unitId;

    public HttpClient As(Role role) => clients[role];

    public void Dispose()
    {
        foreach (var client in clients.Values)
        {
            client.Dispose();
        }
    }
}
