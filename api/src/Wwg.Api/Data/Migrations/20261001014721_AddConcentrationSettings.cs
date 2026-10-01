using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddConcentrationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CavalryLimit",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                // Existing campaigns get the rules' limits.
                defaultValue: 160
            );

            migrationBuilder.AddColumn<string>(
                name: "CavalryLimitTypes",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "InfantryLimit",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 200
            );

            migrationBuilder.AddColumn<string>(
                name: "InfantryLimitTypes",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "CavalryLimit", table: "Campaigns");

            migrationBuilder.DropColumn(name: "CavalryLimitTypes", table: "Campaigns");

            migrationBuilder.DropColumn(name: "InfantryLimit", table: "Campaigns");

            migrationBuilder.DropColumn(name: "InfantryLimitTypes", table: "Campaigns");
        }
    }
}
