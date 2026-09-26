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

            // Repõe o Caminho a partir da Essência 2 quando ela é um dos antigos Caminhos
            // (EssenciaBasica: 4 = Alma, 5 = Vida, 6 = Mundano), pra um rollback não perdê-lo.
            foreach (var table in new[] { "NpcAffinities", "CharacterAffinities" })
                migrationBuilder.Sql(
                    $"UPDATE \"{table}\" SET \"CaminhoNome\" = CASE \"SegundaEssencia\" WHEN 4 THEN 'Alma' WHEN 5 THEN 'Vida' WHEN 6 THEN 'Mundano' END WHERE \"SegundaEssencia\" >= 4;");
        }
    }
}
