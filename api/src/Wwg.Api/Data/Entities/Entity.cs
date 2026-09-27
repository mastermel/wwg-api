namespace Wwg.Api.Data.Entities;

/// <summary>
/// Base class for the app's own entities (Identity's <see cref="AppUser"/> can't derive from it).
/// </summary>
internal abstract class Entity : IHasCreatedAt, IHasUpdatedAt
{
    /// <summary>
    /// A time-ordered v7 GUID, set on construction. Still order by <see cref="CreatedAt"/> (then
    /// <see cref="Id"/>), never by <see cref="Id"/> alone.
    /// </summary>
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
