using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;
using Wwg.Api.Features.Maps;
using Wwg.Api.Features.Turns;

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
    private static List<Hex> ParsePath(string text) =>
        text.Length == 0
            ? []
            :
            [
                .. text.Split(';')
                    .Select(step => step.Split(','))
                    .Select(parts => new Hex(
                        int.Parse(parts[0], CultureInfo.InvariantCulture),
                        int.Parse(parts[1], CultureInfo.InvariantCulture)
                    )),
            ];

    public void Configure(EntityTypeBuilder<UnitOrder> builder)
    {
        builder.HasIndex(o => new { o.ArmyTurnId, o.UnitId }).IsUnique();
        builder.Property(o => o.Kind).HasMaxLength(8);
        // The path as compact text, "q,r;q,r;…" (empty for none): it's only ever read whole.
        builder
            .Property(o => o.Path)
            .HasConversion(
                path =>
                    string.Join(';', path.Select(h => FormattableString.Invariant($"{h.Q},{h.R}"))),
                text => ParsePath(text),
                new ValueComparer<List<Hex>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    path => path.Aggregate(0, (hash, h) => HashCode.Combine(hash, h)),
                    path => path.ToList()
                )
            )
            .HasMaxLength(Movement.MaxSteps * 12);
        builder
            .HasOne(o => o.ArmyTurn)
            .WithMany()
            .HasForeignKey(o => o.ArmyTurnId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(o => o.ArmyUnit)
            .WithMany()
            .HasForeignKey(o => o.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class ArmyTurnEventConfiguration : IEntityTypeConfiguration<ArmyTurnEvent>
{
    public void Configure(EntityTypeBuilder<ArmyTurnEvent> builder)
    {
        builder.Property(e => e.Kind).HasMaxLength(16);
        builder.Property(e => e.Note).HasMaxLength(2000);
        builder
            .HasOne(e => e.ArmyTurn)
            .WithMany()
            .HasForeignKey(e => e.ArmyTurnId)
            .OnDelete(DeleteBehavior.Cascade);
        // Deleting a user keeps what they did, without them.
        builder
            .HasOne(e => e.ByUser)
            .WithMany()
            .HasForeignKey(e => e.ByUserId)
            .OnDelete(DeleteBehavior.SetNull);
        builder
            .HasMany(e => e.UnitNotes)
            .WithOne()
            .HasForeignKey(n => n.ArmyTurnEventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class UnitNoteConfiguration : IEntityTypeConfiguration<UnitNote>
{
    public void Configure(EntityTypeBuilder<UnitNote> builder)
    {
        builder.Property(n => n.Text).HasMaxLength(1000);
        builder
            .HasOne(n => n.ArmyUnit)
            .WithMany()
            .HasForeignKey(n => n.UnitId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
