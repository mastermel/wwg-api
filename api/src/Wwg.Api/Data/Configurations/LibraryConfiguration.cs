using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class FactionConfiguration : IEntityTypeConfiguration<Faction>
{
    public void Configure(EntityTypeBuilder<Faction> builder)
    {
        // Duplicates are allowed (decision 0015); the name is sorted on.
        builder.Property(f => f.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.HasIndex(f => f.Name);
        builder.Property(f => f.Nation).HasMaxLength(32);
    }
}

internal sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.Property(u => u.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(u => u.Type).HasMaxLength(32);
        // A faction with units can't be deleted (the handler says so first).
        builder
            .HasOne(u => u.Faction)
            .WithMany()
            .HasForeignKey(u => u.FactionId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
