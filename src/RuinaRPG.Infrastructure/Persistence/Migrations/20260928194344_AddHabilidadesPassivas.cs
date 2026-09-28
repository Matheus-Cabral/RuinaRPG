using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHabilidadesPassivas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "SpellAbilityBankEntries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requisitos",
                table: "SpellAbilityBankEntries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "NpcSpellAbilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requisitos",
                table: "NpcSpellAbilities",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "CreatureSpellAbilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requisitos",
                table: "CreatureSpellAbilities",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Categoria",
                table: "CharacterSpellAbilities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requisitos",
                table: "CharacterSpellAbilities",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "SpellAbilityBankEntries");

            migrationBuilder.DropColumn(
                name: "Requisitos",
                table: "SpellAbilityBankEntries");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "NpcSpellAbilities");

            migrationBuilder.DropColumn(
                name: "Requisitos",
                table: "NpcSpellAbilities");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "CreatureSpellAbilities");

            migrationBuilder.DropColumn(
                name: "Requisitos",
                table: "CreatureSpellAbilities");

            migrationBuilder.DropColumn(
                name: "Categoria",
                table: "CharacterSpellAbilities");

            migrationBuilder.DropColumn(
                name: "Requisitos",
                table: "CharacterSpellAbilities");
        }
    }
}
