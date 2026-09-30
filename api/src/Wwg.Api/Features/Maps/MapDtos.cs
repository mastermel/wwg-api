using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Maps;

/// <summary>The area a campaign's map is held inside, in degrees (west to east, south to north).</summary>
/// <param name="West">The western edge's longitude.</param>
/// <param name="South">The southern edge's latitude.</param>
/// <param name="East">The eastern edge's longitude (east of West: bounds can't cross 180°).</param>
/// <param name="North">The northern edge's latitude (north of South).</param>
public sealed record MapBounds(
    [property: Range(-180.0, 180.0)] double West,
    [property: Range(-85.0, 85.0)] double South,
    [property: Range(-180.0, 180.0)] double East,
    [property: Range(-85.0, 85.0)] double North
);

/// <summary>Which of the map's layers are shown.</summary>
/// <param name="Roads">Main roads.</param>
/// <param name="Places">Cities, towns and villages.</param>
/// <param name="Water">Rivers, canals, lakes and the sea.</param>
/// <param name="Forests">Woods and forests.</param>
/// <param name="Hills">Hillshading.</param>
/// <param name="Contours">Contour lines.</param>
public sealed record MapLayers(
    bool Roads,
    bool Places,
    bool Water,
    bool Forests,
    bool Hills,
    bool Contours
);

/// <summary>A campaign's map settings.</summary>
/// <param name="Bounds">The area everyone's map is held inside, or null until the Umpire sets it.</param>
/// <param name="LabelLanguage">Place names' language: an ISO 639-1 code, or "local".</param>
/// <param name="DistanceUnit">How distances are shown.</param>
/// <param name="Layers">What the map shows.</param>
/// <param name="HexSize">The hex grid's hexes, in metres across the flats (4828: 3 miles).</param>
public sealed record CampaignMapResponse(
    MapBounds? Bounds,
    string LabelLanguage,
    DistanceUnit DistanceUnit,
    MapLayers Layers,
    int HexSize
);

/// <summary>Changes a campaign's map settings (all of them).</summary>
/// <param name="Bounds">The area everyone's map is held inside, or null for none yet.</param>
/// <param name="LabelLanguage">Place names' language: an ISO 639-1 code from the list, or "local".</param>
/// <param name="DistanceUnit">How distances are shown.</param>
/// <param name="Layers">What the map shows.</param>
/// <param name="HexSize">
/// The hex grid's hexes, in metres across the flats. With the bounds, fixed once the campaign has
/// started.
/// </param>
public sealed record UpdateCampaignMapRequest(
    MapBounds? Bounds,
    [property:
        Required,
        AllowedValues(
            CampaignMaps.LocalLanguage,
            "en",
            "fr",
            "de",
            "es",
            "it",
            "pt",
            "nl",
            "pl",
            "ru",
            "sv",
            "da"
        )
    ]
        string LabelLanguage,
    [property: JsonRequired, EnumDataType(typeof(DistanceUnit))] DistanceUnit DistanceUnit,
    [property: Required] MapLayers Layers,
    [property: JsonRequired, Range(CampaignMap.MinHexSize, CampaignMap.MaxHexSize)] int HexSize
);

/// <summary>A place found by a search, to frame the map on.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Description">Its name with where it is ("Leipzig, Saxony, Germany").</param>
/// <param name="Longitude">Its centre's longitude.</param>
/// <param name="Latitude">Its centre's latitude.</param>
/// <param name="Bounds">Its extent, if the service knows it.</param>
public sealed record PlaceResult(
    string Name,
    string Description,
    double Longitude,
    double Latitude,
    MapBounds? Bounds
);
