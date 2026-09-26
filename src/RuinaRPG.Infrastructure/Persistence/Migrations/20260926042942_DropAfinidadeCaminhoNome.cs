using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DropAfinidadeCaminhoNome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CaminhoNome",
                table: "NpcAffinities");

            migrationBuilder.DropColumn(
                name: "CaminhoNome",
                table: "CharacterAffinities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CaminhoNome",
                table: "NpcAffinities",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CaminhoNome",
                table: "CharacterAffinities",
                type: "text",
                nullable: true);
        }
    }
}
