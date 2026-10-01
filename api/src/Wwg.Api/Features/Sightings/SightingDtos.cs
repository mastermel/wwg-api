using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Sightings;

/// <summary>A unit in a hex another army can see (decision 0020), for the Umpire.</summary>
/// <param name="UnitId">The unit.</param>
/// <param name="ArmyId">Its army.</param>
/// <param name="Name">Its name.</param>
/// <param name="Type">Its type.</param>
/// <param name="Points">Its points.</param>
public sealed record SightedUnitResponse(
    Guid UnitId,
    Guid ArmyId,
    string Name,
    UnitType Type,
    int Points
);

/// <summary>
/// A sighting the closing turn gives (decision 0020): another side's hex an army can see, for the
/// Umpire to shape when starting the next turn.
/// </summary>
/// <param name="ObservingArmyId">The army that sees it.</param>
/// <param name="Q">The hex (axial q).</param>
/// <param name="R">The hex (axial r).</param>
/// <param name="Whereabouts">Where it is, roughly, from the army's nearest unit.</param>
/// <param name="Screened">The observed side's light troops stand in the way: perhaps a screen.</param>
/// <param name="Units">The other side's units there.</param>
public sealed record SightingDueResponse(
    Guid ObservingArmyId,
    int Q,
    int R,
    string Whereabouts,
    bool Screened,
    IReadOnlyList<SightedUnitResponse> Units
);
