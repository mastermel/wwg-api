namespace Wwg.Api.Data.Entities;

/// <summary>Where a report's courier is (decision 0020).</summary>
public enum CourierStatus
{
    /// <summary>On its way.</summary>
    EnRoute,

    /// <summary>Delivered: the recipient has the report.</summary>
    Arrived,

    /// <summary>Stopped by the Umpire (captured, lost): it never arrives.</summary>
    Stopped,
}

/// <summary>
/// Intelligence one army's commander sends an ally (decision 0020): a snapshot of their units, the
/// sightings they'd received, and a note, carried by a courier tracked turn by turn, not drawn.
/// What was sent is copied in as it was then.
/// </summary>
internal sealed class IntelReport : Entity
{
    public Guid FromArmyId { get; set; }

    public Army FromArmy { get; set; } = null!; // Set by EF Core when loaded.

    public Guid ToArmyId { get; set; }

    public Army ToArmy { get; set; } = null!; // Set by EF Core when loaded.

    /// <summary>The turn it was sent in.</summary>
    public int SentTurn { get; set; }

    /// <summary>The turn it arrived for (the recipient has it from then), once it has.</summary>
    public int? ArrivedTurn { get; set; }

    public string? Note { get; set; }

    /// <summary>The sender's units as they were (JSON: name, type, hex, points), if sent.</summary>
    public string? Snapshot { get; set; }

    /// <summary>The sightings sent (the sender's, as they were), copied to the recipient on arrival.</summary>
    public List<Guid> SightingIds { get; set; } = [];

    public CourierStatus Status { get; set; }

    /// <summary>The courier's hex.</summary>
    public int CourierQ { get; set; }

    public int CourierR { get; set; }

    /// <summary>Within two turns' ride when sent: it arrives as the next turn starts (§L).</summary>
    public bool ArrivesNext { get; set; }
}
