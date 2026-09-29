using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wwg.Api.Data.Migrations
{
    /// <summary>
    /// Triggers that make the commander rules (DESIGN §5.1) the database's final guarantee, as the
    /// unique indexes are for theirs: the handlers check them, but two requests can race past the
    /// checks (a Player made Umpire while being given an army).
    /// </summary>
    /// <remarks>
    /// A later migration that rebuilds <c>Armies</c> or <c>CampaignMembers</c> drops the triggers
    /// first and creates them again after (<see cref="CommanderRules"/>).
    /// </remarks>
    public partial class AddCommanderRules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            CommanderRules.Create(migrationBuilder);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            CommanderRules.Drop(migrationBuilder);
    }
}
