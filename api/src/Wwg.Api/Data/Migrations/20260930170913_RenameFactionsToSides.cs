using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameFactionsToSides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A campaign's factions are its sides now (decision 0015): renamed in place. EF's own
            // operations would rebuild Armies for its foreign key, which drops the commander
            // triggers (see AddFactions); SQLite renames tables and columns, and the references
            // to them, without one. Indexes can't be renamed, so they're made again.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Factions" RENAME TO "Sides";
                ALTER TABLE "Armies" RENAME COLUMN "FactionId" TO "SideId";
                DROP INDEX "IX_Factions_CampaignId_Name";
                CREATE UNIQUE INDEX "IX_Sides_CampaignId_Name" ON "Sides" ("CampaignId", "Name");
                DROP INDEX "IX_Armies_FactionId";
                CREATE INDEX "IX_Armies_SideId" ON "Armies" ("SideId");
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "IX_Armies_SideId";
                CREATE INDEX "IX_Armies_FactionId" ON "Armies" ("SideId");
                DROP INDEX "IX_Sides_CampaignId_Name";
                ALTER TABLE "Armies" RENAME COLUMN "SideId" TO "FactionId";
                ALTER TABLE "Sides" RENAME TO "Factions";
                CREATE UNIQUE INDEX "IX_Factions_CampaignId_Name" ON "Factions" ("CampaignId", "Name");
                """
            );
        }
    }
}
