using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Turns;

/// <summary>An order's row, as the queries read it, before its hex's centre is worked out.</summary>
internal sealed record OrderRow(
    Guid UnitId,
    Guid ArmyId,
    int Turn,
    ArmyTurnStatus Status,
    OrderKind Kind,
    int Q,
    int R,
    List<Hex> Path,
    bool ByUmpire,
    double? Progress = null
);

internal static class Positions
{
    /// <summary>The order as the API gives it: its hex, with that hex's centre.</summary>
    public static UnitPosition Of(HexGrid grid, OrderRow row)
    {
        var (latitude, longitude) = grid.Centre(new Hex(row.Q, row.R));
        return new UnitPosition(
            row.UnitId,
            row.ArmyId,
            row.Turn,
            row.Status,
            row.Kind,
            row.Q,
            row.R,
            latitude,
            longitude,
            row.Path,
            row.ByUmpire,
            row.Progress
        );
    }
}
