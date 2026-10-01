using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTipoDeRuna : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "RuneBankEntries",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "NpcRunes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tipo",
                table: "CharacterRunes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "RuneBankEntries");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "NpcRunes");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "CharacterRunes");
        }
    }
}
