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
    /// distance unit, layers and movement limits. Before the Umpire saves any, the defaults.
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
    /// north; every unit type has a movement limit, once.
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
            map.ShowContours
        ) = (
            request.Layers.Roads,
            request.Layers.Places,
            request.Layers.Water,
            request.Layers.Forests,
            request.Layers.Hills,
            request.Layers.Contours
        );

        await StageLimitsAsync(db, id, request.MovementLimits, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return TypedResults.Ok(await LoadAsync(db, id, cancellationToken));
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

    /// <summary>Adds or changes the campaign's limits (saved with the rest).</summary>
    private static async Task StageLimitsAsync(
        WwgDbContext db,
        Guid id,
        IReadOnlyList<MovementLimitDto> wantedLimits,
        CancellationToken cancellationToken
    )
    {
        var limits = await db
            .MovementLimits.Where(l => l.CampaignId == id)
            .ToListAsync(cancellationToken);
        foreach (var wanted in wantedLimits)
        {
            var limit = limits.SingleOrDefault(l => l.UnitType == wanted.UnitType);
            if (limit is null)
            {
                db.MovementLimits.Add(
                    new MovementLimit
                    {
                        CampaignId = id,
                        UnitType = wanted.UnitType,
                        Metres = wanted.Metres,
                    }
                );
            }
            else
            {
                limit.Metres = wanted.Metres;
            }
        }
    }

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

        var types = request.MovementLimits.Select(l => l.UnitType).ToList();
        if (
            types.Count != types.Distinct().Count()
            || !Enum.GetValues<UnitType>().All(types.Contains)
        )
        {
            errors["movementLimits"] = ["Give every type of unit one movement limit."];
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
        var saved = await db
            .MovementLimits.AsNoTracking()
            .Where(l => l.CampaignId == campaignId)
            .ToDictionaryAsync(l => l.UnitType, l => l.Metres, cancellationToken);
        var limits = Enum.GetValues<UnitType>()
            .Select(type => new MovementLimitDto(
                type,
                saved.TryGetValue(type, out var metres) ? metres : CampaignMaps.DefaultMetres[type]
            ))
            .ToList();

        if (map is null)
        {
            return new CampaignMapResponse(
                null,
                CampaignMaps.DefaultLanguage,
                CampaignMaps.DefaultDistanceUnit,
                CampaignMaps.DefaultLayers,
                CampaignMap.DefaultHexSize,
                limits
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
                map.ShowContours
            ),
            map.HexSize,
            limits
        );
    }
}
