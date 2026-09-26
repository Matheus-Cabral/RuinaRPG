using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.CharacterSheets;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAfinidadeSegundaEssencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SegundaEssencia",
                table: "NpcAffinities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegundaEssenciaValor",
                table: "NpcAffinities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegundaEssencia",
                table: "CharacterAffinities",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SegundaEssenciaValor",
                table: "CharacterAffinities",
                type: "integer",
                nullable: true);

            // Converte as linhas antigas (Elemento + Caminho + Sub-Elemento escolhido) pra duas
            // Essências. Gerado a partir de MatrizElemental — ver
            // docs/superpowers/specs/2026-09-26-afinidades-duas-essencias-design.md, "Conversion".
            foreach (var table in new[] { "CharacterAffinities", "NpcAffinities" })
            {
                foreach (var e1 in Enum.GetValues<Elemento>())
                foreach (var e2 in Enum.GetValues<EssenciaBasica>())
                {
                    if (MatrizElemental.Intersecao(e1, e2) is not { } sub)
                        continue;

                    // 1) O Sub-Elemento gravado sai deste par → a Essência 2 é o que o reproduz.
                    migrationBuilder.Sql(
                        $"UPDATE \"{table}\" SET \"SegundaEssencia\" = {(int)e2} " +
                        $"WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = {(int)e1} AND \"SubElemento\" = {(int)sub};");

                    // 2) Sem Sub-Elemento, mas com um Caminho que cruza com o Elemento → vira a Essência 2 e gera o Sub-Elemento.
                    if (e2 >= EssenciaBasica.Alma)
                        migrationBuilder.Sql(
                            $"UPDATE \"{table}\" SET \"SegundaEssencia\" = {(int)e2}, \"SubElemento\" = {(int)sub} " +
                            $"WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = {(int)e1} AND \"CaminhoNome\" = '{e2}';");
                }
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SegundaEssencia",
                table: "NpcAffinities");

            migrationBuilder.DropColumn(
                name: "SegundaEssenciaValor",
                table: "NpcAffinities");

            migrationBuilder.DropColumn(
                name: "SegundaEssencia",
                table: "CharacterAffinities");

            migrationBuilder.DropColumn(
                name: "SegundaEssenciaValor",
                table: "CharacterAffinities");
        }
    }
}
