using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFactions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Factions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(
                        type: "TEXT",
                        maxLength: 100,
                        nullable: false,
                        collation: "NOCASE"
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Factions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Factions_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            // Not AddColumn + AddForeignKey: EF would rebuild Armies for the foreign key (at the
            // end of the migration), which drops the commander triggers, and fails while
            // CampaignMembers' trigger refers to Armies. SQLite adds a column with its foreign key
            // in place. The model snapshot records the same foreign key.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Armies" ADD "FactionId" TEXT NULL
                    REFERENCES "Factions" ("Id") ON DELETE SET NULL;
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Armies_FactionId",
                table: "Armies",
                column: "FactionId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Factions_CampaignId_Name",
                table: "Factions",
                columns: new[] { "CampaignId", "Name" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Dropping the column rebuilds Armies at the end of this migration, so the commander
            // triggers go first and can't be recreated here: roll a deploy back with the
            // pre-migration backup instead (docs/operations.md).
            CommanderRules.Drop(migrationBuilder);

            migrationBuilder.DropIndex(name: "IX_Armies_FactionId", table: "Armies");

            migrationBuilder.DropColumn(name: "FactionId", table: "Armies");

            migrationBuilder.DropTable(name: "Factions");
        }
    }
}
