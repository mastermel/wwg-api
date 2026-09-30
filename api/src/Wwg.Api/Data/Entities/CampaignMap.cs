namespace Wwg.Api.Data.Entities;

/// <summary>How distances are shown (they're stored in metres).</summary>
public enum DistanceUnit
{
    Kilometres,
    Miles,
}

/// <summary>
/// A campaign's map settings (one per campaign, made when the Umpire first saves them): the area
/// everyone's map is held inside, what the map shows, and how distances read.
/// </summary>
internal sealed class CampaignMap : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The bounds, in degrees; all null until the Umpire sets the area.</summary>
    public double? West { get; set; }

    public double? South { get; set; }

    public double? East { get; set; }

    public double? North { get; set; }

    /// <summary>Place names' language: an ISO 639-1 code, or "local" for each place's own.</summary>
    public required string LabelLanguage { get; set; }

    public DistanceUnit DistanceUnit { get; set; }

    /// <summary>The smallest, default and largest hex size: 500 m, 3 miles, 50 km across.</summary>
    public const int MinHexSize = 500,
        DefaultHexSize = 4828,
        MaxHexSize = 50_000;

    /// <summary>
    /// The hex grid's hexes, in metres across the flats (decision 0014). With the bounds, it lays
    /// out the grid, so neither changes once the campaign has started.
    /// </summary>
    public int HexSize { get; set; } = DefaultHexSize;

    public bool ShowRoads { get; set; }

    public bool ShowPlaces { get; set; }

    public bool ShowWater { get; set; }

    public bool ShowForests { get; set; }

    public bool ShowHills { get; set; }

    public bool ShowContours { get; set; }
}

/// <summary>How far a unit of a type can move in one turn, in a campaign.</summary>
internal sealed class MovementLimit : Entity
{
    public Guid CampaignId { get; set; }

    public Campaign Campaign { get; set; } = null!; // Set by EF Core when loaded.

    public UnitType UnitType { get; set; }

    /// <summary>The straight-line distance, in metres.</summary>
    public int Metres { get; set; }
}
