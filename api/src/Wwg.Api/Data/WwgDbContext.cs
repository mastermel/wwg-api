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

    public DbSet<ArmyUnit> ArmyUnits => Set<ArmyUnit>();

    public DbSet<Side> Sides => Set<Side>();

    /// <summary>The library's factions (decision 0015).</summary>
    public DbSet<Faction> Factions => Set<Faction>();

    /// <summary>The library's units (decision 0015).</summary>
    public DbSet<Unit> Units => Set<Unit>();

    public DbSet<ArmyFaction> ArmyFactions => Set<ArmyFaction>();

    public DbSet<CampaignMap> CampaignMaps => Set<CampaignMap>();

    /// <summary>The grid's hexes with terrain (decision 0014).</summary>
    public DbSet<HexCell> HexCells => Set<HexCell>();

    /// <summary>The grid's edges with roads or rivers (decision 0014).</summary>
    public DbSet<HexEdge> HexEdges => Set<HexEdge>();

    /// <summary>Hexes' actual terrain, found on request (decision 0016).</summary>
    public DbSet<HexDetail> HexDetails => Set<HexDetail>();

    public DbSet<HexDetailReveal> HexDetailReveals => Set<HexDetailReveal>();

    /// <summary>Campaigns' own movement tables (step 44); none: the rules'.</summary>
    public DbSet<MovementRate> MovementRates => Set<MovementRate>();

    public DbSet<CampaignTurn> CampaignTurns => Set<CampaignTurn>();

    public DbSet<ArmyTurn> ArmyTurns => Set<ArmyTurn>();

    public DbSet<UnitOrder> UnitOrders => Set<UnitOrder>();

    public DbSet<ArmyTurnEvent> ArmyTurnEvents => Set<ArmyTurnEvent>();

    public DbSet<UnitNote> UnitNotes => Set<UnitNote>();

    public DbSet<PointsChange> PointsChanges => Set<PointsChange>();

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
