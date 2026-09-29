using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

// History isn't deleted by accident: an army with turns, or a unit with orders, can't be deleted
// on its own (NO ACTION; the handlers explain why first). Deleting the campaign deletes it all.
// NO ACTION rather than RESTRICT: SQLite checks it at the end of the statement, so a campaign's
// cascade can delete its armies and their turns in either order.

internal sealed class CampaignTurnConfiguration : IEntityTypeConfiguration<CampaignTurn>
{
    public void Configure(EntityTypeBuilder<CampaignTurn> builder)
    {
        builder.HasIndex(t => new { t.CampaignId, t.Number }).IsUnique();
        builder
            .HasOne(t => t.Campaign)
            .WithMany()
            .HasForeignKey(t => t.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ArmyTurnConfiguration : IEntityTypeConfiguration<ArmyTurn>
{
    public void Configure(EntityTypeBuilder<ArmyTurn> builder)
    {
        builder.HasIndex(t => new { t.CampaignTurnId, t.ArmyId }).IsUnique();
        builder.Property(t => t.Status).HasMaxLength(16);
        builder
            .HasOne(t => t.CampaignTurn)
            .WithMany()
            .HasForeignKey(t => t.CampaignTurnId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(t => t.Army)
            .WithMany()
            .HasForeignKey(t => t.ArmyId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class UnitOrderConfiguration : IEntityTypeConfiguration<UnitOrder>
{
    public void Configure(EntityTypeBuilder<UnitOrder> builder)
    {
        builder.HasIndex(o => new { o.ArmyTurnId, o.UnitId }).IsUnique();
        builder.Property(o => o.Kind).HasMaxLength(8);
        builder
            .HasOne(o => o.ArmyTurn)
            .WithMany()
            .HasForeignKey(o => o.ArmyTurnId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(o => o.Unit)
            .WithMany()
            .HasForeignKey(o => o.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
