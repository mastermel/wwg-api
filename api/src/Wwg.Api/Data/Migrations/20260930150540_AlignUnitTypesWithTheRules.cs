using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AlignUnitTypesWithTheRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The rule book's movement classes (decision 0014): heavy infantry is line infantry,
            // and skirmishers light infantry (the rules have no class of their own for them).
            migrationBuilder.Sql(
                "UPDATE \"Units\" SET \"Type\" = 'LineInfantry' WHERE \"Type\" = 'HeavyInfantry';"
            );
            migrationBuilder.Sql(
                "UPDATE \"Units\" SET \"Type\" = 'LightInfantry' WHERE \"Type\" = 'Skirmishers';"
            );
            // The movement limits follow; light infantry already has its own.
            migrationBuilder.Sql(
                "UPDATE \"MovementLimits\" SET \"UnitType\" = 'LineInfantry' "
                    + "WHERE \"UnitType\" = 'HeavyInfantry';"
            );
            migrationBuilder.Sql(
                "DELETE FROM \"MovementLimits\" WHERE \"UnitType\" = 'Skirmishers';"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Skirmishers can't be told from light infantry any more: they stay light infantry.
            // The new types have no old equivalent; they become the nearest old one.
            migrationBuilder.Sql(
                "UPDATE \"Units\" SET \"Type\" = CASE \"Type\" "
                    + "WHEN 'LineInfantry' THEN 'HeavyInfantry' "
                    + "WHEN 'Engineers' THEN 'HeavyInfantry' "
                    + "WHEN 'Partisans' THEN 'LightInfantry' "
                    + "WHEN 'Scouts' THEN 'LightCavalry' "
                    + "WHEN 'MediumCavalry' THEN 'HeavyCavalry' "
                    + "WHEN 'SupplyTrain' THEN 'FootArtillery' "
                    + "WHEN 'SiegeArtillery' THEN 'FootArtillery' "
                    + "ELSE \"Type\" END;"
            );
            migrationBuilder.Sql(
                "DELETE FROM \"MovementLimits\" WHERE \"UnitType\" NOT IN ('LineInfantry', "
                    + "'LightInfantry', 'LightCavalry', 'HeavyCavalry', 'FootArtillery', "
                    + "'HorseArtillery');"
            );
            migrationBuilder.Sql(
                "UPDATE \"MovementLimits\" SET \"UnitType\" = 'HeavyInfantry' "
                    + "WHERE \"UnitType\" = 'LineInfantry';"
            );
        }
    }
}
