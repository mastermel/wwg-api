using System.ComponentModel.DataAnnotations;
using Wwg.Api.Infrastructure;

namespace Wwg.Api.Features.Factions;

/// <summary>Adds a faction (a side) to the campaign.</summary>
/// <param name="Name">The faction's name, unique in the campaign.</param>
public sealed record CreateFactionRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>Renames a faction.</summary>
/// <param name="Name">The faction's new name, unique in the campaign.</param>
public sealed record RenameFactionRequest(
    [property: Trimmed, Required, StringLength(100)] string Name
);

/// <summary>A side in the campaign. Every member sees them all.</summary>
/// <param name="Id">The faction's ID.</param>
/// <param name="Name">Its name.</param>
/// <param name="ArmyCount">How many armies are in it.</param>
public sealed record FactionResponse(Guid Id, string Name, int ArmyCount);
