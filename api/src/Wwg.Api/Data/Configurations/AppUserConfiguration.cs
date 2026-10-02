using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        // The turned-off emails as their names, "TurnStarted;PlayerJoined" (empty for none).
        builder
            .Property(u => u.MutedEmails)
            .HasConversion(
                kinds => string.Join(';', kinds),
                text =>
                    text.Length == 0
                        ? new List<EmailKind>()
                        : text.Split(';', StringSplitOptions.None)
                            .Select(Enum.Parse<EmailKind>)
                            .ToList(),
                new ValueComparer<List<EmailKind>>(
                    (a, b) => a != null && b != null && a.SequenceEqual(b),
                    kinds => kinds.Aggregate(0, (hash, k) => HashCode.Combine(hash, k)),
                    kinds => kinds.ToList()
                )
            )
            .HasMaxLength(200);
    }
}
