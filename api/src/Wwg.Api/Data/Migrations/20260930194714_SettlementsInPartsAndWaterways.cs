using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SettlementsInPartsAndWaterways : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Waterway",
                table: "HexEdges",
                type: "TEXT",
                maxLength: 4,
                nullable: false,
                defaultValue: "None"
            );

            migrationBuilder.AddColumn<string>(
                name: "Capital",
                table: "HexCells",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "None"
            );

            migrationBuilder.AddColumn<bool>(
                name: "Fortress",
                table: "HexCells",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "HexCells",
                type: "TEXT",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "SettlementSize",
                table: "HexCells",
                type: "TEXT",
                maxLength: 8,
                nullable: false,
                defaultValue: "None"
            );

            migrationBuilder.AddColumn<bool>(
                name: "Walled",
                table: "HexCells",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            // Decision 0016: the old single settlement onto its parts (a walled city of unknown
            // size becomes a walled city).
            migrationBuilder.Sql(
                """
                UPDATE "HexCells"
                SET "SettlementSize" = CASE "Settlement"
                        WHEN 'SmallCity' THEN 'Town'
                        WHEN 'LargeCity' THEN 'City'
                        WHEN 'WalledCity' THEN 'City'
                        ELSE 'None'
                    END,
                    "Walled" = "Settlement" = 'WalledCity',
                    "Fortress" = "Settlement" = 'Fortress';
                """
            );

            migrationBuilder.DropColumn(name: "Settlement", table: "HexCells");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Settlement",
                table: "HexCells",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "None"
            );

            // The largest part back as the one settlement.
            migrationBuilder.Sql(
                """
                UPDATE "HexCells"
                SET "Settlement" = CASE
                    WHEN "Fortress" THEN 'Fortress'
                    WHEN "Walled" THEN 'WalledCity'
                    WHEN "SettlementSize" = 'City' THEN 'LargeCity'
                    WHEN "SettlementSize" = 'Town' THEN 'SmallCity'
                    ELSE 'None'
                END;
                """
            );

            migrationBuilder.DropColumn(name: "Waterway", table: "HexEdges");

            migrationBuilder.DropColumn(name: "Capital", table: "HexCells");

            migrationBuilder.DropColumn(name: "Fortress", table: "HexCells");

            migrationBuilder.DropColumn(name: "Name", table: "HexCells");

            migrationBuilder.DropColumn(name: "SettlementSize", table: "HexCells");

            migrationBuilder.DropColumn(name: "Walled", table: "HexCells");
        }
    }
}
