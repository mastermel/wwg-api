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

    /// <summary>The first turn's day (step 45), or null until the Umpire sets it.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>The first turn's time of day; each turn after is the next (three to a day).</summary>
    public TurnPart FirstTurnPart { get; set; }

    /// <summary>
    /// Whose infantry move a flat hex further each Morning (the rules, §E.1); null: the rules'
    /// usual ones (<see cref="TurnParts.MorningNations"/>).
    /// </summary>
    public List<Nation>? MorningNations { get; set; }

    /// <summary>Whose infantry move a flat hex less each Afternoon; null: Russia and Austria.</summary>
    public List<Nation>? AfternoonNations { get; set; }
}

/// <summary>A turn's time of day (the rules, §D.1): 8 hours each, three to a day.</summary>
public enum TurnPart
{
    /// <summary>06:00 to 14:00.</summary>
    Morning,

    /// <summary>14:00 to 22:00.</summary>
    Afternoon,

    /// <summary>22:00 to 06:00: moving by night counts towards a forced march.</summary>
    Night,
}

/// <summary>The rules' time of day for turns, and who it favours.</summary>
internal static class TurnParts
{
    /// <summary>
    /// French infantry and their usual allies (§E.1: not Westphalians, Saxons, Spanish,
    /// Portuguese, Neapolitans or Danes): a flat hex further each Morning.
    /// </summary>
    public static readonly IReadOnlyList<Nation> MorningNations =
    [
        Nation.France,
        Nation.Bavaria,
        Nation.Wurttemberg,
        Nation.Baden,
        Nation.Warsaw,
        Nation.Italy,
        Nation.Holland,
    ];

    /// <summary>Russian and Austrian infantry: a flat hex less each Afternoon.</summary>
    public static readonly IReadOnlyList<Nation> AfternoonNations = [Nation.Russia, Nation.Austria];

    /// <summary>Turn <paramref name="number"/>'s time of day and day (turn 0, setup, has none).</summary>
    public static (TurnPart Part, DateOnly? Date)? Of(
        TurnPart first,
        DateOnly? startDate,
        int number
    )
    {
        if (number < 1)
        {
            return null;
        }

        var index = (int)first + number - 1;
        return ((TurnPart)(index % 3), startDate?.AddDays(index / 3));
    }
}
