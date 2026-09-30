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
    }
}
