using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class LinkArmyUnitsToTheLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "ArmyUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.AddColumn<Guid>(
                name: "UnitId",
                table: "ArmyUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateTable(
                name: "ArmyFactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    FactionId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmyFactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArmyFactions_Armies_ArmyId",
                        column: x => x.ArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ArmyFactions_Factions_FactionId",
                        column: x => x.FactionId,
                        principalTable: "Factions",
                        principalColumn: "Id"
                    );
                }
            );

            // Decision 0015: every army unit goes into the library, in a faction named after its
            // army's nation ("Unsorted" for none), which its army then takes units from. Before
            // the unique index, while every row's UnitId is still empty. No GUIDs are made in
            // SQL: a library unit takes its army unit's ID, a nation's faction the lowest of its
            // units' IDs, and an army's choice of that faction the army's ID (each unique in its
            // own table). A Manager can rename, merge and sort them out afterwards.
            migrationBuilder.Sql(
                """
                INSERT INTO "Factions" ("Id", "Name", "Nation", "CreatedAt", "UpdatedAt")
                SELECT MIN(u."Id"),
                    CASE a."Nation"
                        WHEN 'None' THEN 'Unsorted'
                        WHEN 'Wurttemberg' THEN 'Württemberg'
                        WHEN 'Warsaw' THEN 'Duchy of Warsaw'
                        WHEN 'Italy' THEN 'Kingdom of Italy'
                        WHEN 'Ottoman' THEN 'Ottoman Empire'
                        ELSE a."Nation"
                    END,
                    a."Nation",
                    strftime('%Y-%m-%d %H:%M:%f', 'now'),
                    strftime('%Y-%m-%d %H:%M:%f', 'now')
                FROM "ArmyUnits" AS u
                JOIN "Armies" AS a ON a."Id" = u."ArmyId"
                GROUP BY a."Nation";

                INSERT INTO "Units"
                    ("Id", "FactionId", "Name", "Type", "FightingFactor", "Points", "CreatedAt",
                    "UpdatedAt")
                SELECT u."Id", f."Id", u."Name", u."Type", u."FightingFactor", u."Points",
                    u."CreatedAt", u."UpdatedAt"
                FROM "ArmyUnits" AS u
                JOIN "Armies" AS a ON a."Id" = u."ArmyId"
                JOIN "Factions" AS f
                    ON f."Nation" = a."Nation" AND f."Id" IN (SELECT "Id" FROM "ArmyUnits");

                UPDATE "ArmyUnits"
                SET "UnitId" = "Id",
                    "CampaignId" = (
                        SELECT a."CampaignId" FROM "Armies" AS a WHERE a."Id" = "ArmyUnits"."ArmyId"
                    );

                INSERT INTO "ArmyFactions" ("Id", "ArmyId", "FactionId", "CreatedAt", "UpdatedAt")
                SELECT a."Id", a."Id", f."Id",
                    strftime('%Y-%m-%d %H:%M:%f', 'now'),
                    strftime('%Y-%m-%d %H:%M:%f', 'now')
                FROM "Armies" AS a
                JOIN "Factions" AS f
                    ON f."Nation" = a."Nation" AND f."Id" IN (SELECT "Id" FROM "ArmyUnits")
                WHERE EXISTS (SELECT 1 FROM "ArmyUnits" AS u WHERE u."ArmyId" = a."Id");
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyUnits_CampaignId_UnitId",
                table: "ArmyUnits",
                columns: new[] { "CampaignId", "UnitId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyUnits_UnitId",
                table: "ArmyUnits",
                column: "UnitId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyFactions_ArmyId_FactionId",
                table: "ArmyFactions",
                columns: new[] { "ArmyId", "FactionId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyFactions_FactionId",
                table: "ArmyFactions",
                column: "FactionId"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ArmyUnits_Campaigns_CampaignId",
                table: "ArmyUnits",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_ArmyUnits_Units_UnitId",
                table: "ArmyUnits",
                column: "UnitId",
                principalTable: "Units",
                principalColumn: "Id"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ArmyUnits_Campaigns_CampaignId",
                table: "ArmyUnits"
            );

            migrationBuilder.DropForeignKey(name: "FK_ArmyUnits_Units_UnitId", table: "ArmyUnits");

            migrationBuilder.DropTable(name: "ArmyFactions");

            migrationBuilder.DropIndex(name: "IX_ArmyUnits_CampaignId_UnitId", table: "ArmyUnits");

            migrationBuilder.DropIndex(name: "IX_ArmyUnits_UnitId", table: "ArmyUnits");

            migrationBuilder.DropColumn(name: "CampaignId", table: "ArmyUnits");

            migrationBuilder.DropColumn(name: "UnitId", table: "ArmyUnits");
        }
    }
}
