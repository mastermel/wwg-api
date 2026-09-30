using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGridLayer : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShowGrid",
                table: "CampaignMaps",
                type: "INTEGER",
                nullable: false,
                // Existing maps show the grid, as new ones do.
                defaultValue: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // In place: EF's DropColumn would rebuild the table (see AddFactions).
            migrationBuilder.Sql("ALTER TABLE \"CampaignMaps\" DROP COLUMN \"ShowGrid\";");
        }
    }
}
