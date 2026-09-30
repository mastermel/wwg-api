using System.ComponentModel.DataAnnotations;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Sides;

/// <summary>Adds a side to the campaign.</summary>
/// <param name="Name">The side's name, unique in the campaign.</param>
public sealed record CreateSideRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>Renames a side.</summary>
/// <param name="Name">The side's new name, unique in the campaign.</param>
public sealed record RenameSideRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>A side in the campaign. Every member sees them all.</summary>
/// <param name="Id">The side's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="ArmyCount">How many armies are in it.</param>
public sealed record SideResponse(Guid Id, string Name, int ArmyCount);
