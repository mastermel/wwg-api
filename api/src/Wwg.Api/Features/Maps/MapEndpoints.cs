using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Turns;
using Wwg.Api.Infrastructure;
using Wwg.Api.Infrastructure.Auth;
using Wwg.Api.Infrastructure.Geocoding;

namespace Wwg.Api.Features.Maps;

internal static partial class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder app)
    {
        var map = app.MapGroup("/api/campaigns/{id:guid}/map").WithTags("Maps");
        map.MapGet("", GetCampaignMapAsync)
            .WithName("GetCampaignMap")
            .RequireCampaignAccess(CampaignAccess.Member);
        map.MapPut("", UpdateCampaignMapAsync)
            .WithName("UpdateCampaignMap")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .ProducesProblem(StatusCodes.Status409Conflict);

        app.MapGet("/api/campaigns/{id:guid}/places", SearchPlacesAsync)
            .WithName("SearchPlaces")
            .WithTags("Maps")
            .RequireCampaignAccess(CampaignAccess.Umpire)
            .RequireRateLimiting(RateLimiting.PlacesPolicy)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    /// <summary>
    /// The campaign's map settings (every member): its bounds (null until set), label language,
    /// distance unit, layers and hex size. Before the Umpire saves any, the defaults.
    /// </summary>
    internal static async Task<Ok<CampaignMapResponse>> GetCampaignMapAsync(
        Guid id,
        WwgDbContext db,
        CancellationToken cancellationToken
    ) => TypedResults.Ok(await LoadAsync(db, id, cancellationToken));

    /// <summary>
    /// Places matching <paramref name="search"/> (areas and settlements, not addresses), for the
    /// Umpire to frame the map on (Umpire or Admin). 503 if the geocoding service fails.
    /// </summary>
    internal static async Task<Results<Ok<List<PlaceResult>>, ProblemHttpResult>> SearchPlacesAsync(
        Guid id,
        [Required, StringLength(200, MinimumLength = 2)] string search,
        IGeocoder geocoder,
        ILogger<CampaignMap> logger,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<Place> places;
        try
        {
            places = await geocoder.SearchAsync(search.Trim(), cancellationToken);
        }
        catch (Exception exception)
            when (exception is HttpRequestException or TaskCanceledException or JsonException
                && !cancellationToken.IsCancellationRequested
            )
        {
            LogSearchFailed(logger, exception);
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Place search unavailable",
                detail: "Place search isn't working right now. Try again later, or pan and zoom to the area."
            );
        }

        return TypedResults.Ok(
            places
                .Select(p => new PlaceResult(
                    p.Name,
                    p.Description,
                    p.Longitude,
                    p.Latitude,
                    p.Bounds is var (west, south, east, north)
                        ? new MapBounds(west, south, east, north)
                        : null
                ))
                .ToList()
        );
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Place search failed")]
    private static partial void LogSearchFailed(ILogger logger, Exception exception);

    /// <summary>
    /// Changes the campaign's map settings (Umpire or Admin). Bounds run west to east and south to
    /// north. The bounds and hex size lay out the grid: once the campaign has started they're
    /// fixed (409); while setting up, changing them moves each placement to the new hex holding
    /// the old one's centre, and takes it off the map if that's outside the new grid.
    /// </summary>
    internal static async Task<
        Results<Ok<CampaignMapResponse>, ValidationProblem, ProblemHttpResult>
    > UpdateCampaignMapAsync(
        Guid id,
        UpdateCampaignMapRequest request,
        WwgDbContext db,
        CancellationToken cancellationToken
    )
    {
        if (Invalid(request) is { } problem)
        {
            return problem;
        }

        var map = await db.CampaignMaps.SingleOrDefaultAsync(
            m => m.CampaignId == id,
            cancellationToken
        );
        if (map is null)
        {
            map = new CampaignMap { CampaignId = id, LabelLanguage = request.LabelLanguage };
            db.CampaignMaps.Add(map);
        }
        else if (
            GridChanges(map, request) && await TurnRules.HasStartedAsync(db, id, cancellationToken)
        )
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "The grid is fixed",
                detail: "The campaign has started: its area and hex size can't change now."
            );
        }
        else if (GridChanges(map, request))
        {
            await ResnapPlacementsAsync(db, id, map, request, cancellationToken);
        }

        Apply(map, request);
        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
    }

    private static void Apply(CampaignMap map, UpdateCampaignMapRequest request)
    {
        (map.West, map.South, map.East, map.North) = request.Bounds is { } b
            ? (b.West, b.South, b.East, b.North)
            : ((double?)null, (double?)null, (double?)null, (double?)null);
        map.LabelLanguage = request.LabelLanguage;
        map.DistanceUnit = request.DistanceUnit;
        map.HexSize = request.HexSize;
        (
            map.ShowRoads,
            map.ShowPlaces,
            map.ShowWater,
            map.ShowForests,
            map.ShowHills,
            map.ShowContours,
            map.ShowGrid
        ) = (
            request.Layers.Roads,
            request.Layers.Places,
            request.Layers.Water,
            request.Layers.Forests,
            request.Layers.Hills,
            request.Layers.Contours,
            request.Layers.Grid
        );
    }

    /// <summary>
    /// Moves turn 0's placements onto the new grid (not saved): each to the new hex holding its old
    /// hex's centre, or off the map if that's outside the new grid (or there's no area now).
    /// </summary>
    private static async Task ResnapPlacementsAsync(
        WwgDbContext db,
        Guid campaignId,
        CampaignMap map,
        UpdateCampaignMapRequest request,
        CancellationToken cancellationToken
    )
    {
        if (map is not { West: { } west, South: { } south, East: { } east, North: { } north })
        {
            return;
        }

        var before = new HexGrid(new MapBounds(west, south, east, north), map.HexSize);
        var after = request.Bounds is { } bounds ? new HexGrid(bounds, request.HexSize) : null;
        var placements = await db
            .UnitOrders.Where(o =>
                o.ArmyTurn.Army.CampaignId == campaignId && o.ArmyTurn.CampaignTurn.Number == 0
            )
            .ToListAsync(cancellationToken);
        foreach (var placement in placements)
        {
            var (latitude, longitude) = before.Centre(new Hex(placement.Q, placement.R));
            var hex = after?.HexAt(latitude, longitude);
            if (hex is { } moved && after!.Contains(moved)) // after is set when hex is.
            {
                (placement.Q, placement.R) = (moved.Q, moved.R);
            }
            else
            {
                db.UnitOrders.Remove(placement);
            }
        }
    }

    /// <summary>Whether the request moves the grid: other bounds, or another hex size.</summary>
    private static bool GridChanges(CampaignMap map, UpdateCampaignMapRequest request) =>
        map.HexSize != request.HexSize
        || (map.West, map.South, map.East, map.North)
            != (
                request.Bounds?.West,
                request.Bounds?.South,
                request.Bounds?.East,
                request.Bounds?.North
            );

    /// <summary>The rules DataAnnotations can't express, as a validation problem; null if none broken.</summary>
    private static ValidationProblem? Invalid(UpdateCampaignMapRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.Bounds is { } bounds)
        {
            if (bounds.West >= bounds.East)
            {
                errors["bounds.east"] = ["The eastern edge must be east of the western one."];
            }

            if (bounds.South >= bounds.North)
            {
                errors["bounds.north"] = ["The northern edge must be north of the southern one."];
            }
        }

        return errors.Count == 0 ? null : TypedResults.ValidationProblem(errors);
    }

    private static async Task<CampaignMapResponse> LoadAsync(
        WwgDbContext db,
        Guid campaignId,
        CancellationToken cancellationToken
    )
    {
        var map = await db
            .CampaignMaps.AsNoTracking()
            .SingleOrDefaultAsync(m => m.CampaignId == campaignId, cancellationToken);
        if (map is null)
        {
            return new CampaignMapResponse(
                null,
                CampaignMaps.DefaultLanguage,
                CampaignMaps.DefaultDistanceUnit,
                CampaignMaps.DefaultLayers,
                CampaignMap.DefaultHexSize
            );
        }

        return new CampaignMapResponse(
            map is { West: { } west, South: { } south, East: { } east, North: { } north }
                ? new MapBounds(west, south, east, north)
                : null,
            map.LabelLanguage,
            map.DistanceUnit,
            new MapLayers(
                map.ShowRoads,
                map.ShowPlaces,
                map.ShowWater,
                map.ShowForests,
                map.ShowHills,
                map.ShowContours,
                map.ShowGrid
            ),
            map.HexSize
        );
    }
}
