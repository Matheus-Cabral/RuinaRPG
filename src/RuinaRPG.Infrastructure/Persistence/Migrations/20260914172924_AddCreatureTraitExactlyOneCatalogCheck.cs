using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureTraitExactlyOneCatalogCheck : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_CreatureTraits_ExactlyOneTraitSource",
                table: "CreatureTraits",
                sql: "num_nonnulls(\"TraitId\", \"CreatureExclusiveTraitId\") = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CreatureTraits_ExactlyOneTraitSource",
                table: "CreatureTraits");
        }
    }
}
