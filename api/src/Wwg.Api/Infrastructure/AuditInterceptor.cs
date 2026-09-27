using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Infrastructure;

/// <summary>Sets <c>CreatedAt</c> / <c>UpdatedAt</c> from the injected clock on every save.</summary>
internal sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result
    )
    {
        SetAuditFields(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        SetAuditFields(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private void SetAuditFields(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow().UtcDateTime;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Added && entry.Entity is IHasCreatedAt created)
            {
                created.CreatedAt = now;
            }

            if (
                entry.State is EntityState.Added or EntityState.Modified
                && entry.Entity is IHasUpdatedAt updated
            )
            {
                updated.UpdatedAt = now;
            }
        }
    }
}
