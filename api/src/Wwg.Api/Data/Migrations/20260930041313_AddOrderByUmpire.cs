using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderByUmpire : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ByUmpire",
                table: "UnitOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // In place: EF's DropColumn would rebuild the table (see AddFactions).
            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"ByUmpire\";");
        }
    }
}
