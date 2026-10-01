using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIntelReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntelReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    FromArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ToArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SentTurn = table.Column<int>(type: "INTEGER", nullable: false),
                    ArrivedTurn = table.Column<int>(type: "INTEGER", nullable: true),
                    Note = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Snapshot = table.Column<string>(type: "TEXT", nullable: true),
                    SightingIds = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CourierQ = table.Column<int>(type: "INTEGER", nullable: false),
                    CourierR = table.Column<int>(type: "INTEGER", nullable: false),
                    ArrivesNext = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntelReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntelReports_Armies_FromArmyId",
                        column: x => x.FromArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_IntelReports_Armies_ToArmyId",
                        column: x => x.ToArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_IntelReports_FromArmyId",
                table: "IntelReports",
                column: "FromArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_IntelReports_ToArmyId",
                table: "IntelReports",
                column: "ToArmyId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IntelReports");
        }
    }
}
