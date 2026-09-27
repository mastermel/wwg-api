using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Wwg.Api.Data;

/// <summary>
/// SQLite returns <see cref="DateTime"/>s as <see cref="DateTimeKind.Unspecified"/>, which would
/// serialize without a <c>Z</c> and be read as local time. Every stored timestamp is UTC, so mark
/// it as such when read.
/// </summary>
internal sealed class UtcDateTimeConverter()
    : ValueConverter<DateTime, DateTime>(
        value => value,
        value => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    );
