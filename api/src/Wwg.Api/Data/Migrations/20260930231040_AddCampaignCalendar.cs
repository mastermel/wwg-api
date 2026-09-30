using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignCalendar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AfternoonNations",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "FirstTurnPart",
                table: "Campaigns",
                type: "TEXT",
                maxLength: 16,
                nullable: false,
                defaultValue: "Morning"
            );

            migrationBuilder.AddColumn<string>(
                name: "MorningNations",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateOnly>(
                name: "StartDate",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AfternoonNations", table: "Campaigns");

            migrationBuilder.DropColumn(name: "FirstTurnPart", table: "Campaigns");

            migrationBuilder.DropColumn(name: "MorningNations", table: "Campaigns");

            migrationBuilder.DropColumn(name: "StartDate", table: "Campaigns");
        }
    }
}
