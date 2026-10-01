using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSightings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Sightings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ObservingArmyId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Q = table.Column<int>(type: "INTEGER", nullable: false),
                    R = table.Column<int>(type: "INTEGER", nullable: false),
                    ShowsHex = table.Column<bool>(type: "INTEGER", nullable: false),
                    Whereabouts = table.Column<string>(
                        type: "TEXT",
                        maxLength: 200,
                        nullable: false
                    ),
                    ArmyIds = table.Column<string>(type: "TEXT", nullable: true),
                    UnitTypes = table.Column<string>(type: "TEXT", nullable: true),
                    Strength = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Size = table.Column<string>(type: "TEXT", maxLength: 16, nullable: true),
                    Points = table.Column<int>(type: "INTEGER", nullable: true),
                    ByUmpire = table.Column<bool>(type: "INTEGER", nullable: false),
                    SharedByArmyId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sightings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sightings_Armies_ObservingArmyId",
                        column: x => x.ObservingArmyId,
                        principalTable: "Armies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Sightings_ObservingArmyId_Turn",
                table: "Sightings",
                columns: new[] { "ObservingArmyId", "Turn" }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Sightings");
        }
    }
}
