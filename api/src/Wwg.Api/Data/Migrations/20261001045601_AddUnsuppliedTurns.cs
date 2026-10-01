using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnsuppliedTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnsuppliedTurns",
                table: "ArmyUnits",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "UnsuppliedTurns", table: "ArmyUnits");
        }
    }
}
