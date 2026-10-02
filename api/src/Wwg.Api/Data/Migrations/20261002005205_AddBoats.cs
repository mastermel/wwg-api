using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBoats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Boats",
                table: "UnitOrders",
                type: "TEXT",
                maxLength: 3700,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<Guid>(
                name: "CarrierId",
                table: "UnitOrders",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "BoatCapacity",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                // Chart #11's boat, as new campaigns have.
                defaultValue: 14
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Boats", table: "UnitOrders");

            migrationBuilder.DropColumn(name: "CarrierId", table: "UnitOrders");

            migrationBuilder.DropColumn(name: "BoatCapacity", table: "Campaigns");
        }
    }
}
