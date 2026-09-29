using Microsoft.EntityFrameworkCore.Migrations;

namespace Wwg.Api.Data.Migrations;

/// <summary>
/// The triggers behind the commander rules (DESIGN §5.1), first added by
/// <see cref="AddCommanderRules"/>.
/// </summary>
/// <remarks>
/// EF rebuilds a SQLite table for some changes (e.g. adding a foreign key). A rebuild drops the
/// table's triggers, and fails outright while another table's trigger refers to it. So a
/// migration that rebuilds <c>Armies</c> or <c>CampaignMembers</c> calls <see cref="Drop"/> first
/// and <see cref="Create"/> after. <c>DatabaseTests</c> fails if the triggers are missing.
/// </remarks>
internal static class CommanderRules
{
    private const string CommanderIsAPlayerInTheCampaign = """
        WHEN NEW."CommanderId" IS NOT NULL AND NOT EXISTS (
            SELECT 1 FROM "CampaignMembers" AS m
            WHERE m."Id" = NEW."CommanderId"
                AND m."CampaignId" = NEW."CampaignId"
                AND m."Role" = 'Player'
        )
        BEGIN
            SELECT RAISE(ABORT, 'An army''s commander must be a Player in its campaign.');
        END;
        """;

    public static void Create(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            $"""
            CREATE TRIGGER "Armies_CommanderIsAPlayer_Insert"
            BEFORE INSERT ON "Armies"
            {CommanderIsAPlayerInTheCampaign}
            """
        );
        migrationBuilder.Sql(
            $"""
            CREATE TRIGGER "Armies_CommanderIsAPlayer_Update"
            BEFORE UPDATE OF "CommanderId", "CampaignId" ON "Armies"
            {CommanderIsAPlayerInTheCampaign}
            """
        );
        migrationBuilder.Sql(
            """
            CREATE TRIGGER "CampaignMembers_UmpireCommandsNoArmy"
            BEFORE UPDATE OF "Role" ON "CampaignMembers"
            WHEN NEW."Role" = 'Umpire'
                AND EXISTS (SELECT 1 FROM "Armies" AS a WHERE a."CommanderId" = NEW."Id")
            BEGIN
                SELECT RAISE(ABORT, 'The Umpire can''t command an army.');
            END;
            """
        );
    }

    public static void Drop(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "CampaignMembers_UmpireCommandsNoArmy";""");
        migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "Armies_CommanderIsAPlayer_Update";""");
        migrationBuilder.Sql("""DROP TRIGGER IF EXISTS "Armies_CommanderIsAPlayer_Insert";""");
    }
}
