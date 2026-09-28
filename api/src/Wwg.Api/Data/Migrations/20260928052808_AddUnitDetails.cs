using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Units made before these fields get valid values (FF 1, no points, Heavy
            // Infantry), for the Umpire to correct.
            migrationBuilder.AddColumn<int>(
                name: "FightingFactor",
                table: "Units",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Units",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Units",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "HeavyInfantry"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "FightingFactor", table: "Units");

            migrationBuilder.DropColumn(name: "Points", table: "Units");

            migrationBuilder.DropColumn(name: "Type", table: "Units");
        }
    }
}
