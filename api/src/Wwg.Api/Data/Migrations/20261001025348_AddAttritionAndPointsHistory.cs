using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAttritionAndPointsHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AttritionCarry",
                table: "ArmyUnits",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0
            );

            migrationBuilder.CreateTable(
                name: "PointsChanges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ArmyUnitId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Turn = table.Column<int>(type: "INTEGER", nullable: false),
                    Change = table.Column<int>(type: "INTEGER", nullable: false),
                    PointsAfter = table.Column<int>(type: "INTEGER", nullable: false),
                    Reason = table.Column<string>(type: "TEXT", maxLength: 16, nullable: false),
                    Note = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ByUserId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PointsChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PointsChanges_ArmyUnits_ArmyUnitId",
                        column: x => x.ArmyUnitId,
                        principalTable: "ArmyUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_PointsChanges_AspNetUsers_ByUserId",
                        column: x => x.ByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PointsChanges_ArmyUnitId_Turn",
                table: "PointsChanges",
                columns: new[] { "ArmyUnitId", "Turn" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PointsChanges_ByUserId",
                table: "PointsChanges",
                column: "ByUserId"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PointsChanges");

            migrationBuilder.DropColumn(name: "AttritionCarry", table: "ArmyUnits");
        }
    }
}
