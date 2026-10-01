using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

namespace Wwg.Api.Features.Sightings;

/// <summary>Another side's hex an army can see, where a turn leaves the units.</summary>
/// <param name="ObservingArmyId">The army that sees it.</param>
/// <param name="At">The hex.</param>
/// <param name="Observer">Its unit nearest the hex, for where it is roughly.</param>
/// <param name="Screened">The observed side's light troops stand in the way: perhaps a screen.</param>
/// <param name="Units">The other side's units there.</param>
internal sealed record SightCandidate(
    Guid ObservingArmyId,
    Hex At,
    UnitPlace Observer,
    bool Screened,
    List<UnitPlace> Units
);

/// <summary>
/// What units can see (the rules, §K.1; decision 0020), by the map's terrain only: from flat ground
/// the next hex, from a low hill 2, a high hill 3, a mountain 4; a hill hex blocks the view beyond
/// it unless the observer stands higher.
/// </summary>
internal static class Sight
{
    /// <summary>The types that can screen (§K.2).</summary>
    public static readonly IReadOnlySet<UnitType> ScreeningTypes = new HashSet<UnitType>
    {
        UnitType.LightInfantry,
        UnitType.LightCavalry,
        UnitType.MediumCavalry,
    };

    public static int Elevation(Terrain terrain) =>
        terrain switch
        {
            Terrain.LowHill => 1,
            Terrain.HighHill => 2,
            Terrain.Mountain => 3,
            _ => 0,
        };

    /// <summary>Whether a unit in one hex can see into another.</summary>
    public static bool Sees(Func<Hex, Terrain> ground, Hex from, Hex to) =>
        Between(ground, from, to) is not null;

    /// <summary>
    /// The hexes between two (neither end), if the view from one reaches the other; else null.
    /// </summary>
    private static List<Hex>? Between(Func<Hex, Terrain> ground, Hex from, Hex to)
    {
        var eye = Elevation(ground(from));
        var distance = from.Distance(to);
        if (distance > eye + 1)
        {
            return null;
        }

        var between = Line(from, to).Skip(1).SkipLast(1).ToList();
        // Only an observer above flat ground sees past the next hex: a hill as high blocks it.
        return between.Any(hex => Elevation(ground(hex)) >= eye) ? null : between;
    }

    /// <summary>The hexes on the straight line from one hex to another, both ends included.</summary>
    public static IEnumerable<Hex> Line(Hex from, Hex to)
    {
        var steps = from.Distance(to);
        for (var i = 0; i <= steps; i++)
        {
            // Nudged off the corners, so a line along hex edges always picks the same side.
            var t = steps == 0 ? 0 : (double)i / steps;
            var q = from.Q + ((to.Q - from.Q) * t) + 1e-6;
            var r = from.R + ((to.R - from.R) * t) + 1e-6;
            yield return Round(q, r);
        }
    }

    private static Hex Round(double q, double r)
    {
        var s = -q - r;
        var (rq, rr, rs) = (Math.Round(q), Math.Round(r), Math.Round(s));
        var (dq, dr, ds) = (Math.Abs(rq - q), Math.Abs(rr - r), Math.Abs(rs - s));
        if (dq > dr && dq > ds)
        {
            rq = -rr - rs;
        }
        else if (dr > ds)
        {
            rr = -rq - rs;
        }

        return new Hex((int)rq, (int)rr);
    }

    /// <summary>
    /// Every army's sightings where the units are: each hex of the other side's units that one of
    /// its units can see, with its nearest observer, by army then hex.
    /// </summary>
    public static List<SightCandidate> Candidates(
        IReadOnlyList<UnitPlace> places,
        Func<Hex, Terrain> ground
    )
    {
        var candidates = new List<SightCandidate>();
        foreach (var army in places.GroupBy(p => p.ArmyId))
        {
            var side = army.First().SideId;
            foreach (var hex in places.Where(p => p.SideId != side).GroupBy(p => p.At))
            {
                var seeing = army.Select(o =>
                        (Observer: o, Between: Between(ground, o.At, hex.Key))
                    )
                    .Where(x => x.Between is not null)
                    .OrderBy(x => x.Observer.At.Distance(hex.Key))
                    .ToList();
                if (seeing.Count == 0)
                {
                    continue;
                }

                var screened = seeing.All(x =>
                    x.Between!.Any(h =>
                        places.Any(p =>
                            p.At == h && p.SideId != side && ScreeningTypes.Contains(p.Type)
                        )
                    )
                );
                candidates.Add(
                    new(
                        army.Key,
                        hex.Key,
                        seeing[0].Observer,
                        screened && seeing[0].Between!.Count > 0,
                        [.. hex]
                    )
                );
            }
        }

        return
        [
            .. candidates.OrderBy(c => c.ObservingArmyId).ThenBy(c => c.At.R).ThenBy(c => c.At.Q),
        ];
    }

    /// <summary>Where a hex is from a unit, roughly: "2 hexes north-east of Imperial Guard".</summary>
    public static string Roughly(Hex at, UnitPlace from)
    {
        var distance = from.At.Distance(at);
        if (distance == 0)
        {
            return $"In {from.Name}'s hex";
        }

        // Flat-topped hexes: x east, y south, in hex widths.
        var x = 1.5 * (at.Q - from.At.Q);
        var y = Math.Sqrt(3) * (at.R - from.At.R + ((at.Q - from.At.Q) / 2.0));
        var bearing = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
        string[] compass =
        [
            "north",
            "north-east",
            "east",
            "south-east",
            "south",
            "south-west",
            "west",
            "north-west",
        ];
        var direction = compass[(int)Math.Round(bearing / 45) % 8];
        return $"{distance} {(distance == 1 ? "hex" : "hexes")} {direction} of {from.Name}";
    }
}
