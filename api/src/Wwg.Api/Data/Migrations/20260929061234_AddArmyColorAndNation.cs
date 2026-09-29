using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddArmyColorAndNation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "Armies",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Red"
            );

            migrationBuilder.AddColumn<string>(
                name: "Nation",
                table: "Armies",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "None"
            );

            // Existing armies get different colours, in the order they were created, per campaign.
            migrationBuilder.Sql(
                """
                UPDATE "Armies" SET "Color" = (
                    SELECT CASE (o."Row" - 1) % 8
                        WHEN 0 THEN 'Red' WHEN 1 THEN 'Blue' WHEN 2 THEN 'Green'
                        WHEN 3 THEN 'Orange' WHEN 4 THEN 'Purple' WHEN 5 THEN 'Sky'
                        WHEN 6 THEN 'Gold' ELSE 'Magenta' END
                    FROM (
                        SELECT "Id", ROW_NUMBER() OVER (
                            PARTITION BY "CampaignId" ORDER BY "CreatedAt", "Id"
                        ) AS "Row"
                        FROM "Armies"
                    ) AS o
                    WHERE o."Id" = "Armies"."Id"
                );
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not DropColumn, which rebuilds Armies (dropping the commander triggers): SQLite drops
            // plain columns in place.
            migrationBuilder.Sql("""ALTER TABLE "Armies" DROP COLUMN "Nation";""");
            migrationBuilder.Sql("""ALTER TABLE "Armies" DROP COLUMN "Color";""");
        }
    }
}
