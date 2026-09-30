using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Features.Turns;

/// <summary>One cell of the movement table.</summary>
/// <param name="Class">The movement class.</param>
/// <param name="Ground">The ground.</param>
/// <param name="Hexes">Hexes a turn, in halves; 0 if the class can't cross it.</param>
public sealed record MovementRateDto(
    [property: JsonRequired, EnumDataType(typeof(MovementClass))] MovementClass Class,
    [property: JsonRequired, EnumDataType(typeof(Ground))] Ground Ground,
    [property: JsonRequired, Range(0, MovementRates.MaxHexes)] double Hexes
);

/// <summary>A campaign's movement table (step 44).</summary>
/// <param name="Rates">Every class on every ground.</param>
/// <param name="Rules">Whether it's the rule book's table, unchanged.</param>
public sealed record MovementTableResponse(IReadOnlyList<MovementRateDto> Rates, bool Rules);

/// <summary>The Umpire's movement table: every class on every ground, once.</summary>
/// <param name="Rates">Every class on every ground.</param>
public sealed record SaveMovementTableRequest(
    [property: Required, MaxLength(MovementRates.Cells)] IReadOnlyList<MovementRateDto> Rates
);

internal static class MovementRates
{
    /// <summary>The most hexes a turn a table can give.</summary>
    public const int MaxHexes = 20;

    /// <summary>Every class on every ground.</summary>
    public const int Cells = 5 * 6;
}
