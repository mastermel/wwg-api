using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHexSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "HexSize",
                table: "CampaignMaps",
                type: "INTEGER",
                nullable: false,
                // Existing maps get the default: 3 miles.
                defaultValue: 4828
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // In place: EF's DropColumn would rebuild the table (see AddFactions).
            migrationBuilder.Sql("ALTER TABLE \"CampaignMaps\" DROP COLUMN \"HexSize\";");
        }
    }
}
