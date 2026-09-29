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

internal sealed class MovementLimitConfiguration : IEntityTypeConfiguration<MovementLimit>
{
    public void Configure(EntityTypeBuilder<MovementLimit> builder)
    {
        builder.HasIndex(l => new { l.CampaignId, l.UnitType }).IsUnique();
        builder.Property(l => l.UnitType).HasMaxLength(32);

        // Deleting a campaign deletes its movement limits.
        builder
            .HasOne(l => l.Campaign)
            .WithMany()
            .HasForeignKey(l => l.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
