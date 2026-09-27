namespace Wwg.Api.Data.Entities;

/// <summary>Gets <see cref="CreatedAt"/> set (UTC) when first saved, by the audit interceptor.</summary>
internal interface IHasCreatedAt
{
    DateTime CreatedAt { get; set; }
}

/// <summary>Gets <see cref="UpdatedAt"/> set (UTC) on every save, by the audit interceptor.</summary>
internal interface IHasUpdatedAt
{
    DateTime UpdatedAt { get; set; }
}
