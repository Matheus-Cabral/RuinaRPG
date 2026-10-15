using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCriadaNaFichaToSheetSpellAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CriadaNaFicha",
                table: "NpcSpellAbilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CriadaNaFicha",
                table: "CreatureSpellAbilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "CriadaNaFicha",
                table: "CharacterSpellAbilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CriadaNaFicha",
                table: "NpcSpellAbilities");

            migrationBuilder.DropColumn(
                name: "CriadaNaFicha",
                table: "CreatureSpellAbilities");

            migrationBuilder.DropColumn(
                name: "CriadaNaFicha",
                table: "CharacterSpellAbilities");
        }
    }
}
