using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArmyTurnEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    At = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmyTurnEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArmyTurnEvents_ArmyTurns_ArmyTurnId",
                        column: x => x.ArmyTurnId,
                        principalTable: "ArmyTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ArmyTurnEvents_AspNetUsers_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UnitNotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyTurnEventId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Text = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitNotes_ArmyTurnEvents_ArmyTurnEventId",
                        column: x => x.ArmyTurnEventId,
                        principalTable: "ArmyTurnEvents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UnitNotes_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyTurnEvents_ArmyTurnId",
                table: "ArmyTurnEvents",
                column: "ArmyTurnId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyTurnEvents_ByUserId",
                table: "ArmyTurnEvents",
                column: "ByUserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UnitNotes_ArmyTurnEventId",
                table: "UnitNotes",
                column: "ArmyTurnEventId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_UnitNotes_UnitId",
                table: "UnitNotes",
                column: "UnitId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UnitNotes");

            migrationBuilder.DropTable(name: "ArmyTurnEvents");
        }
    }
}
