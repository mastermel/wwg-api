using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <summary>
    /// Triggers that make the commander rules (DESIGN §5.1) the database's final guarantee, as the
    /// unique indexes are for theirs: the handlers check them, but two requests can race past the
    /// checks (a Player made Umpire while being given an army).
    /// </summary>
    /// <remarks>
    /// EF rebuilds a SQLite table for some changes, which drops its triggers. A test
    /// (<c>DatabaseTests</c>) fails if they're missing, so a later migration that rebuilds
    /// <c>Armies</c> or <c>CampaignMembers</c> has to create them again.
    /// </remarks>
    public partial class AddCommanderRules : Migration
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

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP TRIGGER "CampaignMembers_UmpireCommandsNoArmy";""");
            migrationBuilder.Sql("""DROP TRIGGER "Armies_CommanderIsAPlayer_Update";""");
            migrationBuilder.Sql("""DROP TRIGGER "Armies_CommanderIsAPlayer_Insert";""");
        }
    }
}
