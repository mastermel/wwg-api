using System.Text.Json;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.IntegrationTests.Support;

/// <summary>
/// testdata/movement.json: step costs by the rule book's table (decision 0014), which the
/// front-end's movement.ts is tested against too.
/// </summary>
internal static class MovementFigures
{
    public sealed record Cell(Terrain Terrain, bool Forest);

    public sealed record Edge(RoadQuality Road, bool River, bool Bridge, Waterway Waterway);

    public sealed record StepCase(
        string Name,
        UnitType Type,
        Cell? FromCell,
        Cell? Cell,
        Edge? Edge,
        double? Cost,
        string? ClosedBecause
    );

    private sealed record Figures(List<StepCase> Cases);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly Figures All =
        JsonSerializer.Deserialize<Figures>(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "testdata", "movement.json")),
            Json
        ) ?? throw new InvalidOperationException("No movement figures.");

    public static IReadOnlyList<StepCase> Cases => All.Cases;
}
