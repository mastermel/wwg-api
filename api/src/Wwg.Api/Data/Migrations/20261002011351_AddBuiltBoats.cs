using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBuiltBoats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Afloat",
                table: "Sightings",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "ArmyUnits",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "TEXT"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Afloat", table: "Sightings");

            migrationBuilder.AlterColumn<Guid>(
                name: "UnitId",
                table: "ArmyUnits",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "TEXT",
                oldNullable: true
            );
        }
    }
}
