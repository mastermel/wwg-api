using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class ArmyUnitConfiguration : IEntityTypeConfiguration<ArmyUnit>
{
    public void Configure(EntityTypeBuilder<ArmyUnit> builder)
    {
        // Sorted on in the army's unit list.
        builder.Property(u => u.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(u => u.Type).HasMaxLength(32);

        // Deleting an army (or its campaign) deletes its units.
        builder
            .HasOne(u => u.Army)
            .WithMany()
            .HasForeignKey(u => u.ArmyId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(u => u.Campaign)
            .WithMany()
            .HasForeignKey(u => u.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        // A library unit in use can't be deleted (the handler says so first), and is in at most
        // one army per campaign.
        builder
            .HasOne(u => u.Unit)
            .WithMany()
            .HasForeignKey(u => u.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(u => new { u.CampaignId, u.UnitId }).IsUnique();
    }
}
