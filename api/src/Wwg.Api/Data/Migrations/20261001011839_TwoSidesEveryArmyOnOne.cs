using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TwoSidesEveryArmyOnOne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Making SideId required rebuilds Armies at the end of this migration, which fails
            // while CampaignMembers' trigger refers to Armies, and would drop Armies' own: the
            // commander triggers go first, and the next migration puts them back.
            CommanderRules.Drop(migrationBuilder);

            // Decision 0017: exactly two sides a campaign, every army on one. Sides are added where
            // there are fewer (new GUIDs made once, in a temporary table, so each is fixed), any
            // past the first two by name are merged into the second, and armies on none go to the
            // first.
            migrationBuilder.Sql(
                """
                CREATE TEMP TABLE "NewSides" ("CampaignId" TEXT, "Name" TEXT, "H" TEXT);
                INSERT INTO "NewSides"
                SELECT c."Id", n."Name", hex(randomblob(16))
                FROM "Campaigns" AS c, (SELECT 'Side 1' AS "Name" UNION ALL SELECT 'Side 2') AS n
                WHERE NOT EXISTS (SELECT 1 FROM "Sides" AS s WHERE s."CampaignId" = c."Id");
                INSERT INTO "NewSides"
                SELECT c."Id",
                    CASE WHEN (SELECT s."Name" FROM "Sides" AS s WHERE s."CampaignId" = c."Id")
                            = 'Side 2' COLLATE NOCASE
                        THEN 'Side 1' ELSE 'Side 2' END,
                    hex(randomblob(16))
                FROM "Campaigns" AS c
                WHERE (SELECT COUNT(*) FROM "Sides" AS s WHERE s."CampaignId" = c."Id") = 1;
                INSERT INTO "Sides" ("Id", "CampaignId", "Name", "CreatedAt", "UpdatedAt")
                SELECT substr("H", 1, 8) || '-' || substr("H", 9, 4) || '-' || substr("H", 13, 4)
                        || '-' || substr("H", 17, 4) || '-' || substr("H", 21, 12),
                    "CampaignId", "Name",
                    strftime('%Y-%m-%d %H:%M:%f', 'now'), strftime('%Y-%m-%d %H:%M:%f', 'now')
                FROM "NewSides";
                DROP TABLE "NewSides";

                CREATE TEMP TABLE "RankedSides" AS
                SELECT "Id", "CampaignId",
                    ROW_NUMBER() OVER (
                        PARTITION BY "CampaignId" ORDER BY "Name" COLLATE NOCASE, "Id"
                    ) AS "Rank"
                FROM "Sides";
                UPDATE "Armies"
                SET "SideId" = (
                    SELECT r."Id" FROM "RankedSides" AS r
                    WHERE r."CampaignId" = "Armies"."CampaignId" AND r."Rank" = 2
                )
                WHERE "SideId" IN (SELECT "Id" FROM "RankedSides" WHERE "Rank" > 2);
                UPDATE "Armies"
                SET "SideId" = (
                    SELECT r."Id" FROM "RankedSides" AS r
                    WHERE r."CampaignId" = "Armies"."CampaignId" AND r."Rank" = 1
                )
                WHERE "SideId" IS NULL;
                DELETE FROM "Sides" WHERE "Id" IN (SELECT "Id" FROM "RankedSides" WHERE "Rank" > 2);
                DROP TABLE "RankedSides";
                """
            );

            migrationBuilder.DropForeignKey(name: "FK_Armies_Sides_SideId", table: "Armies");

            migrationBuilder.AlterColumn<Guid>(
                name: "SideId",
                table: "Armies",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Armies_Sides_SideId",
                table: "Armies",
                column: "SideId",
                principalTable: "Sides",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rebuilds Armies too: the triggers go first and can't be recreated here (roll a deploy
            // back with the pre-migration backup instead; docs/operations.md). Merged sides stay
            // merged.
            CommanderRules.Drop(migrationBuilder);

            migrationBuilder.DropForeignKey(name: "FK_Armies_Sides_SideId", table: "Armies");

            migrationBuilder.AlterColumn<Guid>(
                name: "SideId",
                table: "Armies",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT"
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Armies_Sides_SideId",
                table: "Armies",
                column: "SideId",
                principalTable: "Sides",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }
    }
}
