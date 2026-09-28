using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class CampaignMemberConfiguration : IEntityTypeConfiguration<CampaignMember>
{
    public void Configure(EntityTypeBuilder<CampaignMember> builder)
    {
        builder.Property(m => m.Role).HasMaxLength(16);

        // A user is in a campaign at most once.
        builder.HasIndex(m => new { m.CampaignId, m.UserId }).IsUnique();

        // At most one Umpire per campaign. (A campaign can have none, if its Umpire's account was
        // deleted; an Admin then sets a new one.)
        builder
            .HasIndex(m => m.CampaignId)
            .IsUnique()
            .HasFilter($"\"{nameof(CampaignMember.Role)}\" = '{nameof(CampaignRole.Umpire)}'")
            .HasDatabaseName("IX_CampaignMembers_OneUmpirePerCampaign");

        // Deleting a user deletes their memberships; their campaigns are kept.
        builder
            .HasOne(m => m.User)
            .WithMany()
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
