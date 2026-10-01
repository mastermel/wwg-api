using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHoldings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "VictoryPoints",
                table: "HexCells",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "HoldingChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    FromArmyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ToArmyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ByUmpire = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HoldingChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HoldingChanges_Armies_FromArmyId",
                        column: x => x.FromArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_HoldingChanges_Armies_ToArmyId",
                        column: x => x.ToArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_HoldingChanges_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "Holdings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    ArmyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Holdings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Holdings_Armies_ArmyId",
                        column: x => x.ArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_Holdings_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HoldingChanges_CampaignId_Turn",
                table: "HoldingChanges",
                columns: new[] { "CampaignId", "Turn" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HoldingChanges_FromArmyId",
                table: "HoldingChanges",
                column: "FromArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HoldingChanges_ToArmyId",
                table: "HoldingChanges",
                column: "ToArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_ArmyId",
                table: "Holdings",
                column: "ArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Holdings_CampaignId_Q_R",
                table: "Holdings",
                columns: new[] { "CampaignId", "Q", "R" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HoldingChanges");

            migrationBuilder.DropTable(name: "Holdings");

            migrationBuilder.DropColumn(name: "VictoryPoints", table: "HexCells");
        }
    }
}
