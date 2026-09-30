using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTerrain : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HexCells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    Terrain = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Forest = table.Column<bool>(type: "INTEGER", nullable: false),
                    Settlement = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    SetByUmpire = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HexCells", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HexCells_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "HexEdges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    Side = table.Column<string>(type: "TEXT", maxLength: 2, nullable: false),
                    Road = table.Column<string>(type: "TEXT", maxLength: 8, nullable: false),
                    River = table.Column<bool>(type: "INTEGER", nullable: false),
                    Bridge = table.Column<bool>(type: "INTEGER", nullable: false),
                    SetByUmpire = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HexEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HexEdges_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexCells_CampaignId_Q_R",
                table: "HexCells",
                columns: new[] { "CampaignId", "Q", "R" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexEdges_CampaignId_Q_R_Side",
                table: "HexEdges",
                columns: new[] { "CampaignId", "Q", "R", "Side" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HexCells");

            migrationBuilder.DropTable(name: "HexEdges");
        }
    }
}
