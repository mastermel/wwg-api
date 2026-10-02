using Microsoft.AspNetCore.Identity;

namespace Wwg.Api.Data.Entities;

internal sealed class AppUser : IdentityUser<Guid>, IHasCreatedAt
{
    public AppUser()
    {
        Id = Guid.CreateVersion7();
    }

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>The campaign emails they've turned off (decision 0023).</summary>
    public List<EmailKind> MutedEmails { get; set; } = [];
}

/// <summary>
/// The campaign emails a user can turn off (decision 0023). The account's own (welcome,
/// confirmation, password reset, a changed address) always go.
/// </summary>
public enum EmailKind
{
    /// <summary>A commander's: a new turn has started, with what's new for their army.</summary>
    TurnStarted,

    /// <summary>A commander's: the Umpire approved, sent back, reopened or submitted their turn.</summary>
    TurnReviewed,

    /// <summary>A player's: they were given an army to command.</summary>
    ArmyGiven,

    /// <summary>The Umpire's: an army submitted its turn.</summary>
    ArmySubmitted,

    /// <summary>The Umpire's: every army has submitted, and what's waiting to start the next turn.</summary>
    AllSubmitted,

    /// <summary>The Umpire's: a player joined their campaign.</summary>
    PlayerJoined,
}
