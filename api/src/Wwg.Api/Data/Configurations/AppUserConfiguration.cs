using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data.Configurations;

internal sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // Searched and sorted on: NOCASE so "mel" finds "Mel" (ASCII letters only).
        builder.Property(u => u.FirstName).HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(u => u.LastName).HasMaxLength(100).UseCollation("NOCASE");
        builder.Property(u => u.Email).UseCollation("NOCASE");
    }
}
