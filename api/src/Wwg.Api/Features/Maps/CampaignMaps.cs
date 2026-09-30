using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

/// <summary>What a campaign's map starts with, before the Umpire changes it.</summary>
internal static class CampaignMaps
{
    public const string LocalLanguage = "local";

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
        Contours: false,
        Grid: true
    );

    /// <summary>The campaign's hex grid, or null while it has no area.</summary>
    public static async Task<HexGrid?> GridAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var map = await db
            .CampaignMaps.AsNoTracking()
            .Where(m => m.CampaignId == campaignId)
            .Select(m => new
            {
                m.West,
                m.South,
                m.East,
                m.North,
                m.HexSize,
            })
            .SingleOrDefaultAsync(cancellationToken);
        return map is { West: { } west, South: { } south, East: { } east, North: { } north }
            ? new HexGrid(new MapBounds(west, south, east, north), map.HexSize)
            : null;
    }
}
