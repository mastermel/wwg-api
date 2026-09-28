using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaigns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false,
                        collation: "NOCASE"
                    ),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 2000,
                        nullable: true
                    ),
                    JoinCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Role = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignMembers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CampaignMembers_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_CampaignId_UserId",
                table: "CampaignMembers",
                columns: new[] { "CampaignId", "UserId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_OneUmpirePerCampaign",
                table: "CampaignMembers",
                column: "CampaignId",
                unique: true,
                filter: "\"Role\" = 'Umpire'"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignMembers_UserId",
                table: "CampaignMembers",
                column: "UserId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_JoinCode",
                table: "Campaigns",
                column: "JoinCode",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignMembers");

            migrationBuilder.DropTable(name: "Campaigns");
        }
    }
}
