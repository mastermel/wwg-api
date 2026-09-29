using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignMaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    West = table.Column<double>(type: "REAL", nullable: true),
                    South = table.Column<double>(type: "REAL", nullable: true),
                    East = table.Column<double>(type: "REAL", nullable: true),
                    North = table.Column<double>(type: "REAL", nullable: true),
                    LabelLanguage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 8,
                        nullable: false
                    ),
                    DistanceUnit = table.Column<string>(
                        type: "TEXT",
                        maxLength: 16,
                        nullable: false
                    ),
                    ShowRoads = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowPlaces = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowWater = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowForests = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowHills = table.Column<bool>(type: "INTEGER", nullable: false),
                    ShowContours = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMaps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMaps_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "MovementLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UnitType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Metres = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovementLimits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovementLimits_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMaps_CampaignId",
                table: "CampaignMaps",
                column: "CampaignId",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_MovementLimits_CampaignId_UnitType",
                table: "MovementLimits",
                columns: new[] { "CampaignId", "UnitType" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignMaps");

            migrationBuilder.DropTable(name: "MovementLimits");
        }
    }
}
