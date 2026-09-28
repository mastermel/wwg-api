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

        // Deleting a campaign deletes its armies.
        builder
            .HasOne(a => a.Campaign)
            .WithMany()
            .HasForeignKey(a => a.CampaignId)
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
