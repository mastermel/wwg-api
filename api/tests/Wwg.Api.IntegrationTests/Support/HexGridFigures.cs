using System.Text.Json;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// testdata/hex-grid.json: the hex grid's expected figures (decision 0014), from an independent
/// Python reference, which the front-end's hex-grid.ts is tested against too.
/// </summary>
internal static class HexGridFigures
{
    public sealed record LatLon(double Latitude, double Longitude);

    public sealed record GridPoint(double Latitude, double Longitude, int Q, int R, LatLon Centre);

    public sealed record Bounds(double West, double South, double East, double North);

    public sealed record GridCase(string Name, Bounds Bounds, int HexSize, List<GridPoint> Points);

    public sealed record HexRef(int Q, int R);

    public sealed record ResnapHex(int Q, int R, HexRef To, bool InGrid);

    public sealed record ResnapCase(
        Bounds Bounds,
        int FromHexSize,
        int ToHexSize,
        List<ResnapHex> Hexes
    );

    private sealed record Figures(List<GridCase> Cases, ResnapCase Resnap);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Figures All =
        JsonSerializer.Deserialize<Figures>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "testdata", "hex-grid.json")),
            Json
        ) ?? throw new InvalidOperationException("No hex grid figures.");

    public static IReadOnlyList<GridCase> Cases => All.Cases;

    public static ResnapCase Resnap => All.Resnap;
}
