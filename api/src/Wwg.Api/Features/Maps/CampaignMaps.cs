using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

/// <summary>What a campaign's map starts with, before the Umpire changes it.</summary>
internal static class CampaignMaps
{
    public const string LocalLanguage = "local";

    /// <summary>The most a limit can be: 1,000 km.</summary>
    public const int MaxMetres = 1_000_000;

    /// <summary>Places are named in English (or their own name where there's none).</summary>
    public const string DefaultLanguage = "en";

    public const DistanceUnit DefaultDistanceUnit = DistanceUnit.Miles;

    /// <summary>Every layer but contour lines.</summary>
    public static readonly MapLayers DefaultLayers = new(
        Roads: true,
        Places: true,
        Water: true,
        Forests: true,
        Hills: true,
        Contours: false
    );

    /// <summary>
    /// A starting point for a turn of a day or so: a march for infantry, further for cavalry,
    /// less for guns. The Umpire sets the real ones.
    /// </summary>
    public static readonly IReadOnlyDictionary<UnitType, int> DefaultMetres = new Dictionary<
        UnitType,
        int
    >
    {
        [UnitType.HeavyInfantry] = 20_000,
        [UnitType.LightInfantry] = 25_000,
        [UnitType.Skirmishers] = 25_000,
        [UnitType.HeavyCavalry] = 30_000,
        [UnitType.LightCavalry] = 40_000,
        [UnitType.FootArtillery] = 15_000,
        [UnitType.HorseArtillery] = 30_000,
    };
}
