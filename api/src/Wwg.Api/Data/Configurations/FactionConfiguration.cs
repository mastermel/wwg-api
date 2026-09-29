using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class FactionConfiguration : IEntityTypeConfiguration<Faction>
{
    public void Configure(EntityTypeBuilder<Faction> builder)
    {
        // Sorted on, and unique within the campaign regardless of case.
        builder.Property(f => f.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.HasIndex(f => new { f.CampaignId, f.Name }).IsUnique();

        // Deleting a campaign deletes its factions.
        builder
            .HasOne(f => f.Campaign)
            .WithMany()
            .HasForeignKey(f => f.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
