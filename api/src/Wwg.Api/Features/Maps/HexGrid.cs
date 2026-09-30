using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Wwg.Api.Features.Maps;

/// <summary>A hex in a campaign's grid, by its axial coordinates (q east, r south-east).</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Hex([property: JsonRequired] int Q, [property: JsonRequired] int R)
{
    /// <summary>The six neighbours' offsets, clockwise from north: N, NE, SE, S, SW, NW.</summary>
    public static readonly IReadOnlyList<Hex> Directions =
    [
        new(0, -1),
        new(1, -1),
        new(1, 0),
        new(0, 1),
        new(-1, 1),
        new(-1, 0),
    ];

    /// <summary>Whether <paramref name="other"/> is one step away.</summary>
    public bool IsNextTo(Hex other) => Distance(other) == 1;

    /// <summary>How many steps apart two hexes are.</summary>
    public int Distance(Hex other) =>
        (Math.Abs(Q - other.Q) + Math.Abs(Q + R - other.Q - other.R) + Math.Abs(R - other.R)) / 2;
}

/// <summary>
/// A campaign's hex grid (decision 0014): flat-topped hexes <c>size</c> metres across the flats,
/// laid over the area in a local flat projection centred on it (east = R·cos φ₀·Δλ,
/// south = R·Δφ), with hex (0, 0) centred on the area's middle. The grid is every hex whose
/// centre is inside the bounds. The front-end's hex-grid.ts does the same arithmetic; both are
/// tested against testdata/hex-grid.json.
/// </summary>
internal sealed class HexGrid
{
    // The mean Earth radius, as geo.ts uses.
    private const double EarthRadius = 6_371_008.8;

    private readonly MapBounds _bounds;
    private readonly double _lat0;
    private readonly double _lon0;
    private readonly double _k;
    private readonly double _size;

    // The corner-to-centre distance.
    private readonly double _side;

    public HexGrid(MapBounds bounds, int size)
    {
        _bounds = bounds;
        _lat0 = (bounds.South + bounds.North) / 2;
        _lon0 = (bounds.West + bounds.East) / 2;
        _k = EarthRadius * Math.Cos(Radians(_lat0));
        _size = size;
        _side = size / Math.Sqrt(3);
    }

    /// <summary>The hex a point falls in (whether or not it's in the grid).</summary>
    public Hex HexAt(double latitude, double longitude)
    {
        var x = _k * Radians(longitude - _lon0);
        var y = EarthRadius * Radians(_lat0 - latitude);
        var qf = (2.0 / 3 * x) / _side;
        var rf = (-1.0 / 3 * x + Math.Sqrt(3) / 3 * y) / _side;
        var sf = -qf - rf;
        var q = Round(qf);
        var r = Round(rf);
        var s = Round(sf);
        var (dq, dr, ds) = (Math.Abs(q - qf), Math.Abs(r - rf), Math.Abs(s - sf));
        if (dq > dr && dq > ds)
        {
            q = -r - s;
        }
        else if (dr >= ds)
        {
            r = -q - s;
        }

        return new Hex((int)q, (int)r);
    }

    /// <summary>A hex's centre.</summary>
    public (double Latitude, double Longitude) Centre(Hex hex)
    {
        var x = 1.5 * _side * hex.Q;
        var y = _size * (hex.R + hex.Q / 2.0);
        return (_lat0 - Degrees(y / EarthRadius), _lon0 + Degrees(x / _k));
    }

    /// <summary>Whether a hex is in the grid: its centre is inside the bounds.</summary>
    public bool Contains(Hex hex)
    {
        var (latitude, longitude) = Centre(hex);
        return latitude >= _bounds.South
            && latitude <= _bounds.North
            && longitude >= _bounds.West
            && longitude <= _bounds.East;
    }

    private static double Radians(double degrees) => degrees * Math.PI / 180;

    private static double Degrees(double radians) => radians * 180 / Math.PI;

    // Half up, as hex-grid.ts rounds (Math.Round's default rounds halves to even).
    private static double Round(double value) => Math.Floor(value + 0.5);
}
