using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSkillAtributoEscolhido : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AtributoEscolhido",
                table: "NpcSkills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AtributoEscolhido",
                table: "CreatureSkills",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AtributoEscolhido",
                table: "CharacterSkills",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtributoEscolhido",
                table: "NpcSkills");

            migrationBuilder.DropColumn(
                name: "AtributoEscolhido",
                table: "CreatureSkills");

            migrationBuilder.DropColumn(
                name: "AtributoEscolhido",
                table: "CharacterSkills");
        }
    }
}
