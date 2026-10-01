namespace Wwg.Api.Features.Victory;

/// <summary>The Umpire sets who holds a settlement (decision 0021).</summary>
/// <param name="ArmyId">The army that holds it, or null for no one.</param>
public sealed record SetHoldingRequest(Guid? ArmyId);

/// <summary>An army's part of its side's total.</summary>
/// <param name="ArmyId">The army.</param>
/// <param name="Points">What the settlements it holds are worth.</param>
public sealed record ArmyScoreResponse(Guid ArmyId, int Points);

/// <summary>A side's total, and its armies' parts.</summary>
/// <param name="SideId">The side.</param>
/// <param name="Name">Its name.</param>
/// <param name="Points">What its armies' settlements are worth.</param>
/// <param name="Armies">Each of its armies' parts.</param>
public sealed record SideScoreResponse(
    Guid SideId,
    string Name,
    int Points,
    IReadOnlyList<ArmyScoreResponse> Armies
);

/// <summary>A settlement, its worth, and who holds it.</summary>
/// <param name="Q">Its hex (axial q).</param>
/// <param name="R">Its hex (axial r).</param>
/// <param name="Latitude">The hex's centre.</param>
/// <param name="Longitude">The hex's centre.</param>
/// <param name="Name">Its name, if it has one.</param>
/// <param name="Value">What holding it is worth.</param>
/// <param name="ArmyId">Who holds it; null for no one.</param>
public sealed record SettlementScoreResponse(
    int Q,
    int R,
    double Latitude,
    double Longitude,
    string? Name,
    int Value,
    Guid? ArmyId
);

/// <summary>Each side's total after a turn.</summary>
/// <param name="Turn">The turn.</param>
/// <param name="Sides">Each side's total then.</param>
public sealed record TurnScoreResponse(int Turn, IReadOnlyList<SideTotalResponse> Sides);

/// <summary>A side's total, at some turn.</summary>
/// <param name="SideId">The side.</param>
/// <param name="Points">Its total.</param>
public sealed record SideTotalResponse(Guid SideId, int Points);

/// <summary>A settlement changing hands.</summary>
/// <param name="Turn">The turn it happened in (0: set up before the start).</param>
/// <param name="Q">Its hex (axial q).</param>
/// <param name="R">Its hex (axial r).</param>
/// <param name="Name">Its name, if it has one.</param>
/// <param name="Value">What it's worth.</param>
/// <param name="FromArmyId">Who held it before, if anyone.</param>
/// <param name="ToArmyId">Who holds it after, if anyone.</param>
/// <param name="ByUmpire">Set by the Umpire rather than taken.</param>
public sealed record HoldingChangeResponse(
    int Turn,
    int Q,
    int R,
    string? Name,
    int Value,
    Guid? FromArmyId,
    Guid? ToArmyId,
    bool ByUmpire
);

/// <summary>
/// The campaign's victory points (decision 0021), as the viewer may see them: every side's total;
/// their own side's settlements and the changes it made or suffered (the Umpire's, all).
/// </summary>
/// <param name="Sides">Each side's total, with its armies' parts.</param>
/// <param name="Settlements">The settlements the viewer may know the holder of.</param>
/// <param name="Turns">Each side's total after every turn.</param>
/// <param name="Changes">The changes the viewer may know of, oldest first.</param>
public sealed record ScoreboardResponse(
    IReadOnlyList<SideScoreResponse> Sides,
    IReadOnlyList<SettlementScoreResponse> Settlements,
    IReadOnlyList<TurnScoreResponse> Turns,
    IReadOnlyList<HoldingChangeResponse> Changes
);
