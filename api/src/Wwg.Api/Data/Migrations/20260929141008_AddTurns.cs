using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTurns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Number = table.Column<int>(type: "INTEGER", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignTurns_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "ArmyTurns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmyTurns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArmyTurns_Armies_ArmyId",
                        column: x => x.ArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id"
                    );
                    table.ForeignKey(
                        name: "FK_ArmyTurns_CampaignTurns_CampaignTurnId",
                        column: x => x.CampaignTurnId,
                        principalTable: "CampaignTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "UnitOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyTurnId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    Latitude = table.Column<double>(type: "REAL", nullable: false),
                    Longitude = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitOrders_ArmyTurns_ArmyTurnId",
                        column: x => x.ArmyTurnId,
                        principalTable: "ArmyTurns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_UnitOrders_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id"
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyTurns_ArmyId",
                table: "ArmyTurns",
                column: "ArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ArmyTurns_CampaignTurnId_ArmyId",
                table: "ArmyTurns",
                columns: new[] { "CampaignTurnId", "ArmyId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignTurns_CampaignId_Number",
                table: "CampaignTurns",
                columns: new[] { "CampaignId", "Number" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UnitOrders_ArmyTurnId_UnitId",
                table: "UnitOrders",
                columns: new[] { "ArmyTurnId", "UnitId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_UnitOrders_UnitId",
                table: "UnitOrders",
                column: "UnitId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "UnitOrders");

            migrationBuilder.DropTable(name: "ArmyTurns");

            migrationBuilder.DropTable(name: "CampaignTurns");
        }
    }
}
