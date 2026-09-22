using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSheetAndNpcSheetEstrelaSina : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Estrela",
                table: "NpcSheets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SinaAtual",
                table: "NpcSheets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Estrela",
                table: "CharacterSheets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SinaAtual",
                table: "CharacterSheets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Estrela",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "SinaAtual",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "Estrela",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "SinaAtual",
                table: "CharacterSheets");
        }
    }
}
