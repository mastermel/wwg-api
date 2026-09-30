using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;

namespace Wwg.Api.Features.Turns;

/// <summary>The rule book's movement classes (section XII.E; decision 0014).</summary>
public enum MovementClass
{
    /// <summary>Line infantry, foot artillery, engineers.</summary>
    Infantry,

    /// <summary>Light infantry, partisans.</summary>
    Light,

    /// <summary>Light cavalry, scouts.</summary>
    LightCavalry,

    /// <summary>Medium and heavy cavalry, horse artillery.</summary>
    Cavalry,

    /// <summary>Supply trains, siege artillery.</summary>
    Slow,
}

/// <summary>
/// How far units move in a turn (DESIGN.md §5.2, Phase 11): a turn's budget is 1, and entering a
/// hex costs 1 ÷ the class's rate there, in hexes per turn. Until terrain exists (step 41) every
/// hex is flat ground, with no roads or rivers.
/// </summary>
internal static class Movement
{
    /// <summary>The most steps an order's path can have.</summary>
    public const int MaxSteps = 50;

    /// <summary>A turn's movement.</summary>
    public const double Budget = 1;

    // Sums of thirds don't come to exactly 1.
    private const double Tolerance = 1e-9;

    public static MovementClass ClassOf(UnitType type) =>
        type switch
        {
            UnitType.LineInfantry or UnitType.FootArtillery or UnitType.Engineers =>
                MovementClass.Infantry,
            UnitType.LightInfantry or UnitType.Partisans => MovementClass.Light,
            UnitType.LightCavalry or UnitType.Scouts => MovementClass.LightCavalry,
            UnitType.MediumCavalry or UnitType.HeavyCavalry or UnitType.HorseArtillery =>
                MovementClass.Cavalry,
            UnitType.SupplyTrain or UnitType.SiegeArtillery => MovementClass.Slow,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "No movement class."),
        };

    /// <summary>The rules' rate on flat ground, in hexes per turn.</summary>
    public static double FlatRate(MovementClass movementClass) =>
        movementClass switch
        {
            MovementClass.Infantry => 2,
            MovementClass.Light => 3,
            MovementClass.LightCavalry => 4,
            MovementClass.Cavalry => 3,
            MovementClass.Slow => 1,
            _ => throw new ArgumentOutOfRangeException(nameof(movementClass), movementClass, null),
        };

    /// <summary>What entering <paramref name="to"/> from <paramref name="from"/> costs.</summary>
    public static double StepCost(MovementClass movementClass, Hex from, Hex to) =>
        from.IsNextTo(to) ? 1 / FlatRate(movementClass) : double.PositiveInfinity;

    /// <summary>What a path from <paramref name="start"/> costs: the sum of its steps.</summary>
    public static double PathCost(MovementClass movementClass, Hex start, IReadOnlyList<Hex> path)
    {
        var cost = 0.0;
        var at = start;
        foreach (var step in path)
        {
            cost += StepCost(movementClass, at, step);
            at = step;
        }

        return cost;
    }

    /// <summary>Whether a cost is within a turn's budget.</summary>
    public static bool Affordable(double cost) => cost <= Budget + Tolerance;
}
