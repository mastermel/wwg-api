using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameUnitsToArmyUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // A campaign's units are army units now (decision 0015), renamed in place: SQLite
            // updates the orders' and notes' foreign keys with the table. EF's own operations
            // would drop and recreate the table, and its history with it.
            migrationBuilder.Sql(
                """
                ALTER TABLE "Units" RENAME TO "ArmyUnits";
                DROP INDEX "IX_Units_ArmyId";
                CREATE INDEX "IX_ArmyUnits_ArmyId" ON "ArmyUnits" ("ArmyId");
                """
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP INDEX "IX_ArmyUnits_ArmyId";
                ALTER TABLE "ArmyUnits" RENAME TO "Units";
                CREATE INDEX "IX_Units_ArmyId" ON "Units" ("ArmyId");
                """
            );
        }
    }
}
