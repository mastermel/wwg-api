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

internal sealed class HexDetailConfiguration : IEntityTypeConfiguration<HexDetail>
{
    public void Configure(EntityTypeBuilder<HexDetail> builder)
    {
        builder
            .HasIndex(d => new
            {
                d.CampaignId,
                d.Q,
                d.R,
            })
            .IsUnique();
        builder.Property(d => d.Relief).HasMaxLength(16);
        builder.Property(d => d.Dominant).HasMaxLength(16);
        builder.Property(d => d.Favorability).HasMaxLength(16);

        builder
            .HasOne(d => d.Campaign)
            .WithMany()
            .HasForeignKey(d => d.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting the army that asked keeps what was found.
        builder
            .HasOne(d => d.ForArmy)
            .WithMany()
            .HasForeignKey(d => d.ForArmyId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class HexDetailRevealConfiguration : IEntityTypeConfiguration<HexDetailReveal>
{
    public void Configure(EntityTypeBuilder<HexDetailReveal> builder)
    {
        builder.HasIndex(r => new { r.HexDetailId, r.ArmyId }).IsUnique();
        builder
            .HasOne(r => r.HexDetail)
            .WithMany()
            .HasForeignKey(r => r.HexDetailId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(r => r.Army)
            .WithMany()
            .HasForeignKey(r => r.ArmyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MovementRateConfiguration : IEntityTypeConfiguration<MovementRate>
{
    public void Configure(EntityTypeBuilder<MovementRate> builder)
    {
        builder
            .HasIndex(m => new
            {
                m.CampaignId,
                m.Class,
                m.Ground,
            })
            .IsUnique();
        builder.Property(m => m.Class).HasMaxLength(16);
        builder.Property(m => m.Ground).HasMaxLength(16);
        builder
            .HasOne(m => m.Campaign)
            .WithMany()
            .HasForeignKey(m => m.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
