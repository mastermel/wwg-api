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

    /// <summary>
    /// The unit types counted towards a hex's infantry limit (step 46, decision 0017); null: the
    /// usual ones (<see cref="Concentration.InfantryTypes"/>).
    /// </summary>
    public List<UnitType>? InfantryLimitTypes { get; set; }

    /// <summary>The unit types counted towards the cavalry limit; null: the usual ones.</summary>
    public List<UnitType>? CavalryLimitTypes { get; set; }

    /// <summary>The most points of infantry a side may have in a hex (the rules: 200).</summary>
    public int InfantryLimit { get; set; } = Concentration.InfantryLimit;

    /// <summary>The most points of cavalry a side may have in a hex (the rules: 160).</summary>
    public int CavalryLimit { get; set; } = Concentration.CavalryLimit;

    /// <summary>
    /// How far from its army's supply routes a unit may be and stay supplied, in hexes (step 48,
    /// decision 0019).
    /// </summary>
    public int SupplyReach { get; set; } = SupplyRules.DefaultReach;

    /// <summary>The unit types that don't need supply; null: the usual ones.</summary>
    public List<UnitType>? SupplyExemptTypes { get; set; }

    /// <summary>The nations whose units may live off the land; null: France.</summary>
    public List<Nation>? OffTheLandNations { get; set; }

    /// <summary>Which settlements are worth victory points (step 50, decision 0021).</summary>
    public VictoryPointsMode VictoryPoints { get; set; }

    /// <summary>The points a boat carries (step 51, decision 0022; Chart #11's 14).</summary>
    public int BoatCapacity { get; set; } = BoatRules.DefaultCapacity;
}

/// <summary>Which settlements are worth victory points (decision 0021).</summary>
public enum VictoryPointsMode
{
    /// <summary>Every town, city and fortress, by the rules' table, unless the Umpire sets its value.</summary>
    Rules,

    /// <summary>Only those the Umpire gives points; every other is worth nothing.</summary>
    Chosen,
}

/// <summary>Boats (decision 0022): what each carries, until the Umpire changes it, and who boards.</summary>
internal static class BoatRules
{
    /// <summary>Chart #11's boat holds 14 points.</summary>
    public const int DefaultCapacity = 14,
        MinCapacity = 1,
        MaxCapacity = 100;

    /// <summary>The most boats a unit can need: its most points, a point a boat.</summary>
    public const int MaxBoats = UnitStats.MaxPoints;

    /// <summary>The boats a unit of these points needs: one for each capacity's worth, or part.</summary>
    public static int Needed(int points, int capacity) =>
        Math.Max(1, (points + capacity - 1) / capacity);

    /// <summary>Whether a unit of this type may board boats: not boats, nor supply trains.</summary>
    public static bool CanEmbark(UnitType type) =>
        type is not (UnitType.Boat or UnitType.SupplyTrain);
}

/// <summary>The rules' supply (Campaign, §G), until the Umpire changes it (decision 0019).</summary>
internal static class SupplyRules
{
    public const int DefaultReach = 1,
        MaxReach = 3;

    /// <summary>Partisans, light infantry, scouts and light cavalry (§G.5).</summary>
    public static readonly IReadOnlyList<UnitType> ExemptTypes =
    [
        UnitType.Partisans,
        UnitType.LightInfantry,
        UnitType.Scouts,
        UnitType.LightCavalry,
    ];

    /// <summary>French forces living off the land (§G.5(b)).</summary>
    public static readonly IReadOnlyList<Nation> OffTheLandNations = [Nation.France];
}

/// <summary>
/// The rules' concentration limits (Campaign, §H) and the unit types counted towards them, until
/// the Umpire changes them; the types not counted are free.
/// </summary>
internal static class Concentration
{
    public const int InfantryLimit = 200,
        CavalryLimit = 160;

    /// <summary>The highest limit the Umpire can set.</summary>
    public const int MaxLimit = 10_000;

    public static readonly IReadOnlyList<UnitType> InfantryTypes =
    [
        UnitType.LineInfantry,
        UnitType.LightInfantry,
        UnitType.Engineers,
        UnitType.Partisans,
        UnitType.FootArtillery,
    ];

    public static readonly IReadOnlyList<UnitType> CavalryTypes =
    [
        UnitType.LightCavalry,
        UnitType.MediumCavalry,
        UnitType.HeavyCavalry,
        UnitType.Scouts,
        UnitType.HorseArtillery,
    ];
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
