using System.Security.Cryptography;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

/// <summary>The Umpire's dice (decision 0016). Tests replace them with rolls of their choosing.</summary>
internal interface IDice
{
    /// <summary>One six-sided die: 1 to 6.</summary>
    int D6();
}

internal sealed class Dice : IDice
{
    public int D6() => RandomNumberGenerator.GetInt32(1, 7);
}

/// <summary>What the three dice find in a hex: the rule book's table (Campaign, §C.3, p. 57).</summary>
internal sealed record DetailRoll(
    int RedDie,
    int WhiteDie,
    int? GreenDie,
    DetailRelief Relief,
    bool Scrub,
    bool Village,
    bool Woods,
    bool Forest,
    bool Farms,
    bool Fields,
    bool Streams,
    DominantFeature Dominant,
    Favorability Favorability
);

internal static class DetailTable
{
    /// <summary>
    /// The red die's modifier from the hex's map terrain: +1 for low hills or forest, +2 for high
    /// hills or mountains (the larger, when a hex has both).
    /// </summary>
    public static int Modifier(Terrain terrain, bool forest) =>
        terrain is Terrain.HighHill or Terrain.Mountain ? 2
        : terrain is Terrain.LowHill || forest ? 1
        : 0;

    /// <summary>
    /// Shakes the three dice. The red die gets its modifier (or one off, on a flat hex, if the
    /// Umpire chooses) and is kept to the table's 0–7; the green die only when favourability is
    /// wanted.
    /// </summary>
    public static DetailRoll Roll(IDice dice, int modifier, bool minusOne, bool favorability)
    {
        var red = Math.Clamp(dice.D6() + modifier - (minusOne ? 1 : 0), 0, 7);
        var white = dice.D6();
        int? green = favorability ? dice.D6() : null;
        var (relief, scrub, village, woods, forest, farms, fields, streams) = red switch
        {
            0 => (DetailRelief.Flat, true, false, false, false, false, false, false),
            1 => (DetailRelief.Flat, false, true, true, false, false, false, false),
            2 => (DetailRelief.Flat, false, false, false, false, true, false, true),
            3 => (DetailRelief.Flat, false, false, false, true, false, false, false),
            4 => (DetailRelief.Rolling, false, true, true, false, false, false, false),
            5 => (DetailRelief.Rolling, false, false, false, false, true, true, true),
            6 => (DetailRelief.Hilly, false, false, false, false, true, true, true),
            _ => (DetailRelief.HighHills, false, false, false, true, false, false, true),
        };
        var dominant = white switch
        {
            1 => DominantFeature.SmallCastle,
            3 => DominantFeature.WeakFarmhouse,
            5 => DominantFeature.StrongFarmhouse,
            _ => DominantFeature.None,
        };
        var favor = green switch
        {
            null => Favorability.NotRolled,
            1 => Favorability.Favorable,
            6 => Favorability.Unfavorable,
            _ => Favorability.Neutral,
        };
        return new DetailRoll(
            red,
            white,
            green,
            relief,
            scrub,
            village,
            woods,
            forest,
            farms,
            fields,
            streams,
            dominant,
            favor
        );
    }
}
