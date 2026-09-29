namespace Wwg.Api.Features.Turns;

/// <summary>Distances on the Earth, for movement limits (straight lines: great circles).</summary>
internal static class Geo
{
    /// <summary>The Earth's mean radius, in metres.</summary>
    private const double EarthRadius = 6_371_008.8;

    /// <summary>The great-circle distance between two points in degrees, in metres (haversine).</summary>
    public static double Metres(double lat1, double lon1, double lat2, double lon2)
    {
        static double Radians(double degrees) => degrees * Math.PI / 180;
        var dLat = Radians(lat2 - lat1);
        var dLon = Radians(lon2 - lon1);
        var a =
            (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (
                Math.Cos(Radians(lat1))
                * Math.Cos(Radians(lat2))
                * Math.Sin(dLon / 2)
                * Math.Sin(dLon / 2)
            );
        return 2 * EarthRadius * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
