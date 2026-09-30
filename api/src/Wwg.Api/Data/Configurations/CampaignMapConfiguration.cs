using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class CampaignMapConfiguration : IEntityTypeConfiguration<CampaignMap>
{
    public void Configure(EntityTypeBuilder<CampaignMap> builder)
    {
        builder.HasIndex(m => m.CampaignId).IsUnique();
        builder.Property(m => m.LabelLanguage).HasMaxLength(8);
        builder.Property(m => m.DistanceUnit).HasMaxLength(16);

        // Deleting a campaign deletes its map settings.
        builder
            .HasOne(m => m.Campaign)
            .WithMany()
            .HasForeignKey(m => m.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
