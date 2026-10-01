using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplySettingsAndLivingOffTheLand : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "LivesOffTheLand",
                table: "UnitOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<string>(
                name: "OffTheLandNations",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "SupplyExemptTypes",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "SupplyReach",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                // Existing campaigns get the usual reach (decision 0019).
                defaultValue: 1
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "LivesOffTheLand", table: "UnitOrders");

            migrationBuilder.DropColumn(name: "OffTheLandNations", table: "Campaigns");

            migrationBuilder.DropColumn(name: "SupplyExemptTypes", table: "Campaigns");

            migrationBuilder.DropColumn(name: "SupplyReach", table: "Campaigns");
        }
    }
}
