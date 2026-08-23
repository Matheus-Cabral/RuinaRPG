using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    // MERGE HAZARD: the still-unmerged Compêndio de Regras plan (branch worktree-compendio-de-regras)
    // has its own, independently-generated "AddTraits" migration (20260817003237_AddTraits) creating
    // the same table. Two partial classes of the same name in the same namespace will not compile
    // (CS0111) once that branch merges. When executing/merging that plan: delete its AddTraits
    // migration (this one already exists in main) rather than re-adding a duplicate, and verify its
    // Trait/Polaridade/TraitSeed/TraitSeedParser/TraitSeeder source files are byte-identical to what
    // was already imported here (they were, as of this plan's merge) before assuming no diff is needed.
    public partial class AddTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Traits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Traits", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Traits");
        }
    }
}
