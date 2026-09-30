using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PositionsInHexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Positions become hexes (decision 0014). The columns go in first, then each order's
            // point is converted to the hex holding it, in its campaign's grid (the same
            // arithmetic as HexGrid.cs, rounding halves up), then the points go.
            migrationBuilder.AddColumn<int>(
                name: "Q",
                table: "UnitOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "R",
                table: "UnitOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "Path",
                table: "UnitOrders",
                type: "TEXT",
                maxLength: 600,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.Sql(ToHexes);

            // In place: EF's DropColumn would rebuild the table (see AddFactions).
            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"Latitude\";");
            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"Longitude\";");

            migrationBuilder.DropTable(name: "MovementLimits");
        }

        private const string ToHexes = """
            WITH points AS (
                SELECT o."Id",
                    6371008.8 * cos(radians((m."South" + m."North") / 2))
                        * radians(o."Longitude" - (m."West" + m."East") / 2) AS x,
                    6371008.8 * radians((m."South" + m."North") / 2 - o."Latitude") AS y,
                    m."HexSize" / sqrt(3) AS side
                FROM "UnitOrders" o
                JOIN "ArmyTurns" t ON t."Id" = o."ArmyTurnId"
                JOIN "Armies" a ON a."Id" = t."ArmyId"
                JOIN "CampaignMaps" m ON m."CampaignId" = a."CampaignId"
                WHERE m."West" IS NOT NULL
            ),
            fractions AS (
                SELECT "Id", (2.0 / 3 * x) / side AS qf, (-1.0 / 3 * x + sqrt(3) / 3 * y) / side AS rf
                FROM points
            ),
            rounded AS (
                SELECT "Id", qf, rf, -qf - rf AS sf,
                    floor(qf + 0.5) AS q, floor(rf + 0.5) AS r, floor(-qf - rf + 0.5) AS s
                FROM fractions
            )
            UPDATE "UnitOrders" SET
                "Q" = CASE
                    WHEN abs(h.q - h.qf) > abs(h.r - h.rf) AND abs(h.q - h.qf) > abs(h.s - h.sf)
                        THEN -h.r - h.s
                    ELSE h.q END,
                "R" = CASE
                    WHEN abs(h.q - h.qf) > abs(h.r - h.rf) AND abs(h.q - h.qf) > abs(h.s - h.sf)
                        THEN h.r
                    WHEN abs(h.r - h.rf) >= abs(h.s - h.sf) THEN -h.q - h.s
                    ELSE h.r END
            FROM rounded h
            WHERE h."Id" = "UnitOrders"."Id";
            """;

        // Back from hexes: each order's point is its hex's centre.
        private const string ToPoints = """
            UPDATE "UnitOrders" SET
                "Latitude" = g.lat0 - degrees(m."HexSize" * ("UnitOrders"."R" + "UnitOrders"."Q" / 2.0) / 6371008.8),
                "Longitude" = g.lon0 + degrees(1.5 * m."HexSize" / sqrt(3) * "UnitOrders"."Q"
                    / (6371008.8 * cos(radians(g.lat0))))
            FROM "ArmyTurns" t
            JOIN "Armies" a ON a."Id" = t."ArmyId"
            JOIN "CampaignMaps" m ON m."CampaignId" = a."CampaignId"
            JOIN (
                SELECT "CampaignId", ("South" + "North") / 2 AS lat0, ("West" + "East") / 2 AS lon0
                FROM "CampaignMaps"
            ) g ON g."CampaignId" = m."CampaignId"
            WHERE t."Id" = "UnitOrders"."ArmyTurnId" AND m."West" IS NOT NULL;
            """;

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "UnitOrders",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "UnitOrders",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.Sql(ToPoints);

            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"Path\";");
            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"Q\";");
            migrationBuilder.Sql("ALTER TABLE \"UnitOrders\" DROP COLUMN \"R\";");

            migrationBuilder.CreateTable(
                name: "MovementLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Metres = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitType = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
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
                name: "IX_MovementLimits_CampaignId_UnitType",
                table: "MovementLimits",
                columns: new[] { "CampaignId", "UnitType" },
                unique: true
            );
        }
    }
}
