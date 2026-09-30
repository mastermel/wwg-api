using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class HexCellConfiguration : IEntityTypeConfiguration<HexCell>
{
    public void Configure(EntityTypeBuilder<HexCell> builder)
    {
        builder
            .HasIndex(c => new
            {
                c.CampaignId,
                c.Q,
                c.R,
            })
            .IsUnique();
        builder.Property(c => c.Terrain).HasMaxLength(16);
        builder.Property(c => c.SettlementSize).HasMaxLength(8);
        builder.Property(c => c.Capital).HasMaxLength(8);
        builder.Property(c => c.Name).HasMaxLength(100);

        // Deleting a campaign deletes its terrain.
        builder
            .HasOne(c => c.Campaign)
            .WithMany()
            .HasForeignKey(c => c.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class HexEdgeConfiguration : IEntityTypeConfiguration<HexEdge>
{
    public void Configure(EntityTypeBuilder<HexEdge> builder)
    {
        builder
            .HasIndex(e => new
            {
                e.CampaignId,
                e.Q,
                e.R,
                e.Side,
            })
            .IsUnique();
        builder.Property(e => e.Side).HasMaxLength(2);
        builder.Property(e => e.Road).HasMaxLength(8);
        builder.Property(e => e.Waterway).HasMaxLength(4);

        builder
            .HasOne(e => e.Campaign)
            .WithMany()
            .HasForeignKey(e => e.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
