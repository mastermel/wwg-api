using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVictoryPointsMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VictoryPoints",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                // Existing campaigns count every settlement, as they did.
                defaultValue: "Rules"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "VictoryPoints", table: "Campaigns");
        }
    }
}
