using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddHexDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HexDetails",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    Relief = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Scrub = table.Column<bool>(type: "INTEGER", nullable: false),
                    Village = table.Column<bool>(type: "INTEGER", nullable: false),
                    Woods = table.Column<bool>(type: "INTEGER", nullable: false),
                    Forest = table.Column<bool>(type: "INTEGER", nullable: false),
                    Farms = table.Column<bool>(type: "INTEGER", nullable: false),
                    Fields = table.Column<bool>(type: "INTEGER", nullable: false),
                    Streams = table.Column<bool>(type: "INTEGER", nullable: false),
                    Dominant = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Favorability = table.Column<string>(
                        type: "TEXT",
                        maxLength: 16,
                        nullable: false
                    ),
                    ForArmyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    RedDie = table.Column<int>(type: "INTEGER", nullable: true),
                    WhiteDie = table.Column<int>(type: "INTEGER", nullable: true),
                    GreenDie = table.Column<int>(type: "INTEGER", nullable: true),
                    ShownToAll = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HexDetails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HexDetails_Armies_ForArmyId",
                        column: x => x.ForArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                    table.ForeignKey(
                        name: "FK_HexDetails_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "HexDetailReveals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    HexDetailId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HexDetailReveals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HexDetailReveals_Armies_ArmyId",
                        column: x => x.ArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_HexDetailReveals_HexDetails_HexDetailId",
                        column: x => x.HexDetailId,
                        principalTable: "HexDetails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexDetailReveals_ArmyId",
                table: "HexDetailReveals",
                column: "ArmyId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexDetailReveals_HexDetailId_ArmyId",
                table: "HexDetailReveals",
                columns: new[] { "HexDetailId", "ArmyId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexDetails_CampaignId_Q_R",
                table: "HexDetails",
                columns: new[] { "CampaignId", "Q", "R" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_HexDetails_ForArmyId",
                table: "HexDetails",
                column: "ForArmyId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "HexDetailReveals");

            migrationBuilder.DropTable(name: "HexDetails");
        }
    }
}
