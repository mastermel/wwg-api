using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class ArmyConfiguration : IEntityTypeConfiguration<Army>
{
    public void Configure(EntityTypeBuilder<Army> builder)
    {
        // Sorted on in the army list.
        builder.Property(a => a.Name).HasMaxLength(100).UseCollation("NOCASE");

        // A Player commands at most one army. (SQLite allows many NULLs: unassigned armies.)
        builder.HasIndex(a => a.CommanderId).IsUnique();

        builder.Property(a => a.Color).HasMaxLength(16);
        builder.Property(a => a.Nation).HasMaxLength(32);

        // Deleting a campaign deletes its armies.
        builder
            .HasOne(a => a.Campaign)
            .WithMany()
            .HasForeignKey(a => a.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        // Sides go only with their campaign, which takes its armies with it (decision 0017).
        builder
            .HasOne(a => a.Side)
            .WithMany()
            .HasForeignKey(a => a.SideId)
            .OnDelete(DeleteBehavior.Cascade);

        // The commander leaving (or being removed, or deleted) leaves the army unassigned; it and
        // its units are kept.
        builder
            .HasOne(a => a.Commander)
            .WithMany()
            .HasForeignKey(a => a.CommanderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class DepotConfiguration : IEntityTypeConfiguration<Depot>
{
    public void Configure(EntityTypeBuilder<Depot> builder)
    {
        builder.Property(d => d.Kind).HasMaxLength(16);
        builder.Property(d => d.Name).HasMaxLength(100);
        // Deleting an army deletes its depots.
        builder
            .HasOne(d => d.Army)
            .WithMany()
            .HasForeignKey(d => d.ArmyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
