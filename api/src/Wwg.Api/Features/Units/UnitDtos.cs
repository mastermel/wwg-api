using System.ComponentModel.DataAnnotations;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Units;

/// <summary>Adds a unit to an army.</summary>
/// <param name="Name">The unit's name.</param>
public sealed record CreateUnitRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>Renames a unit.</summary>
/// <param name="Name">The unit's new name.</param>
public sealed record RenameUnitRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>A unit in an army.</summary>
/// <param name="Id">The unit's ID.</param>
/// <param name="ArmyId">The army it's in.</param>
/// <param name="Name">Its name.</param>
public sealed record UnitResponse(Guid Id, Guid ArmyId, string Name);
