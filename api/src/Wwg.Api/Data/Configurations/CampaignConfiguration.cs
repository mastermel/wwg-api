using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class CampaignConfiguration : IEntityTypeConfiguration<Campaign>
{
    public void Configure(EntityTypeBuilder<Campaign> builder)
    {
        // Sorted on in the campaign lists: NOCASE so "alpha" and "Beta" sort as people expect.
        builder.Property(c => c.Name).HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(c => c.Description).HasMaxLength(2000);
        builder.Property(c => c.JoinCode).HasMaxLength(32);
        builder.HasIndex(c => c.JoinCode).IsUnique();
        builder.Property(c => c.FirstTurnPart).HasMaxLength(16);
        // Lists of nations, as JSON arrays of their names (like every enum here, by name).
        builder.PrimitiveCollection(c => c.MorningNations).ElementType().HasConversion<string>();
        builder.PrimitiveCollection(c => c.AfternoonNations).ElementType().HasConversion<string>();

        // Deleting a campaign deletes its members (and its armies: see ArmyConfiguration).
        builder
            .HasMany(c => c.Members)
            .WithOne(m => m.Campaign)
            .HasForeignKey(m => m.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
