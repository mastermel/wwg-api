using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class SideConfiguration : IEntityTypeConfiguration<Side>
{
    public void Configure(EntityTypeBuilder<Side> builder)
    {
        // Sorted on, and unique within the campaign regardless of case.
        builder.Property(f => f.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.HasIndex(f => new { f.CampaignId, f.Name }).IsUnique();

        // Deleting a campaign deletes its sides.
        builder
            .HasOne(f => f.Campaign)
            .WithMany()
            .HasForeignKey(f => f.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
