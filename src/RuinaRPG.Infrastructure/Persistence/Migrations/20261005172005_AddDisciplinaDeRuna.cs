using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDisciplinaDeRuna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Disciplina",
                table: "RuneBankEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Disciplina",
                table: "NpcRunes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Disciplina",
                table: "CharacterRunes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Disciplina",
                table: "RuneBankEntries");

            migrationBuilder.DropColumn(
                name: "Disciplina",
                table: "NpcRunes");

            migrationBuilder.DropColumn(
                name: "Disciplina",
                table: "CharacterRunes");
        }
    }
}
