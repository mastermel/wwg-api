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

internal sealed class SightingConfiguration : IEntityTypeConfiguration<Sighting>
{
    public void Configure(EntityTypeBuilder<Sighting> builder)
    {
        builder.Property(s => s.Whereabouts).HasMaxLength(200);
        builder.Property(s => s.Strength).HasMaxLength(16);
        builder.Property(s => s.Size).HasMaxLength(16);
        builder.PrimitiveCollection(s => s.ArmyIds);
        builder.PrimitiveCollection(s => s.UnitTypes).ElementType().HasConversion<string>();
        // Deleting an army deletes what it saw.
        builder
            .HasOne(s => s.ObservingArmy)
            .WithMany()
            .HasForeignKey(s => s.ObservingArmyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(s => new { s.ObservingArmyId, s.Turn });
    }
}

internal sealed class IntelReportConfiguration : IEntityTypeConfiguration<IntelReport>
{
    public void Configure(EntityTypeBuilder<IntelReport> builder)
    {
        builder.Property(r => r.Note).HasMaxLength(1000);
        builder.Property(r => r.Status).HasMaxLength(16);
        builder.PrimitiveCollection(r => r.SightingIds);
        // Deleting either army deletes the reports between them.
        builder
            .HasOne(r => r.FromArmy)
            .WithMany()
            .HasForeignKey(r => r.FromArmyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(r => r.ToArmy)
            .WithMany()
            .HasForeignKey(r => r.ToArmyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class HoldingConfiguration : IEntityTypeConfiguration<Holding>
{
    public void Configure(EntityTypeBuilder<Holding> builder)
    {
        builder
            .HasIndex(h => new
            {
                h.CampaignId,
                h.Q,
                h.R,
            })
            .IsUnique();
        builder
            .HasOne(h => h.Campaign)
            .WithMany()
            .HasForeignKey(h => h.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        // An army that goes leaves what it held to no one.
        builder
            .HasOne(h => h.Army)
            .WithMany()
            .HasForeignKey(h => h.ArmyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class HoldingChangeConfiguration : IEntityTypeConfiguration<HoldingChange>
{
    public void Configure(EntityTypeBuilder<HoldingChange> builder)
    {
        builder.HasIndex(c => new { c.CampaignId, c.Turn });
        builder
            .HasOne(c => c.Campaign)
            .WithMany()
            .HasForeignKey(c => c.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(c => c.FromArmy)
            .WithMany()
            .HasForeignKey(c => c.FromArmyId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasOne(c => c.ToArmy)
            .WithMany()
            .HasForeignKey(c => c.ToArmyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
