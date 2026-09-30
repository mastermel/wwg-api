using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Wwg.Api.Data.Entities;

namespace Wwg.Api.Data;

internal sealed class WwgDbContext(DbContextOptions<WwgDbContext> options)
    : IdentityDbContext<AppUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Campaign> Campaigns => Set<Campaign>();

    public DbSet<CampaignMember> CampaignMembers => Set<CampaignMember>();

    public DbSet<Army> Armies => Set<Army>();

    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<Side> Sides => Set<Side>();

    public DbSet<CampaignMap> CampaignMaps => Set<CampaignMap>();

    public DbSet<CampaignTurn> CampaignTurns => Set<CampaignTurn>();

    public DbSet<ArmyTurn> ArmyTurns => Set<ArmyTurn>();

    public DbSet<UnitOrder> UnitOrders => Set<UnitOrder>();

    public DbSet<ArmyTurnEvent> ArmyTurnEvents => Set<ArmyTurnEvent>();

    public DbSet<UnitNote> UnitNotes => Set<UnitNote>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(WwgDbContext).Assembly);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<Enum>().HaveConversion<string>();
    }
}
