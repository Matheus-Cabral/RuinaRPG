using Microsoft.EntityFrameworkCore.Migrations;

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
            // Essências — ver docs/superpowers/specs/2026-09-26-afinidades-duas-essencias-design.md,
            // "Conversion". Regras, na ordem, pra cada par (Elemento, Essência 2) que se cruza:
            //   1) O Sub-Elemento gravado sai deste par → a Essência 2 é o que o reproduz.
            //   2) (só Caminhos Alma/Vida/Mundano) Sem Sub-Elemento, mas com um Caminho que cruza
            //      com o Elemento → vira a Essência 2 e gera o Sub-Elemento.
            // Instruções congeladas a partir de MatrizElemental em 2026-09-26: uma migration não pode
            // mudar quando o código de domínio muda, então nada aqui referencia MatrizElemental/enums.

            // CharacterAffinities
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 1 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 0;"); // 1) Ar + Agua → Gelo
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 1;"); // 1) Ar + Fogo → Raio
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 4 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 2;"); // 1) Ar + Alma → Prever
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 4, \"SubElemento\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 0 AND \"CaminhoNome\" = 'Alma';"); // 2) Ar + Alma → Prever
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 3;"); // 1) Ar + Mundano → Ecomancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 0 AND \"CaminhoNome\" = 'Mundano';"); // 2) Ar + Mundano → Ecomancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 0 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 0;"); // 1) Agua + Ar → Gelo
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 5;"); // 1) Agua + Terra → Flora
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 4 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 6;"); // 1) Agua + Alma → Purificar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 4, \"SubElemento\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 1 AND \"CaminhoNome\" = 'Alma';"); // 2) Agua + Alma → Purificar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 7;"); // 1) Agua + Mundano → Hemomancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 7 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 1 AND \"CaminhoNome\" = 'Mundano';"); // 2) Agua + Mundano → Hemomancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 0 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 1;"); // 1) Fogo + Ar → Raio
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 8;"); // 1) Fogo + Terra → Ferro
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 5 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 9;"); // 1) Fogo + Vida → Curar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 5, \"SubElemento\" = 9 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 2 AND \"CaminhoNome\" = 'Vida';"); // 2) Fogo + Vida → Curar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 10;"); // 1) Fogo + Mundano → Necromancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 10 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 2 AND \"CaminhoNome\" = 'Mundano';"); // 2) Fogo + Mundano → Necromancia
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 1 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 5;"); // 1) Terra + Agua → Flora
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 8;"); // 1) Terra + Fogo → Ferro
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 5 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 12;"); // 1) Terra + Vida → Aprimorar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 5, \"SubElemento\" = 12 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 3 AND \"CaminhoNome\" = 'Vida';"); // 2) Terra + Vida → Aprimorar
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 13;"); // 1) Terra + Mundano → Invocacao
            migrationBuilder.Sql("UPDATE \"CharacterAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 13 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 3 AND \"CaminhoNome\" = 'Mundano';"); // 2) Terra + Mundano → Invocacao

            // NpcAffinities
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 1 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 0;"); // 1) Ar + Agua → Gelo
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 1;"); // 1) Ar + Fogo → Raio
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 4 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 2;"); // 1) Ar + Alma → Prever
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 4, \"SubElemento\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 0 AND \"CaminhoNome\" = 'Alma';"); // 2) Ar + Alma → Prever
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 0 AND \"SubElemento\" = 3;"); // 1) Ar + Mundano → Ecomancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 0 AND \"CaminhoNome\" = 'Mundano';"); // 2) Ar + Mundano → Ecomancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 0 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 0;"); // 1) Agua + Ar → Gelo
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 5;"); // 1) Agua + Terra → Flora
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 4 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 6;"); // 1) Agua + Alma → Purificar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 4, \"SubElemento\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 1 AND \"CaminhoNome\" = 'Alma';"); // 2) Agua + Alma → Purificar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 1 AND \"SubElemento\" = 7;"); // 1) Agua + Mundano → Hemomancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 7 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 1 AND \"CaminhoNome\" = 'Mundano';"); // 2) Agua + Mundano → Hemomancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 0 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 1;"); // 1) Fogo + Ar → Raio
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 3 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 8;"); // 1) Fogo + Terra → Ferro
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 5 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 9;"); // 1) Fogo + Vida → Curar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 5, \"SubElemento\" = 9 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 2 AND \"CaminhoNome\" = 'Vida';"); // 2) Fogo + Vida → Curar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 2 AND \"SubElemento\" = 10;"); // 1) Fogo + Mundano → Necromancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 10 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 2 AND \"CaminhoNome\" = 'Mundano';"); // 2) Fogo + Mundano → Necromancia
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 1 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 5;"); // 1) Terra + Agua → Flora
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 2 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 8;"); // 1) Terra + Fogo → Ferro
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 5 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 12;"); // 1) Terra + Vida → Aprimorar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 5, \"SubElemento\" = 12 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 3 AND \"CaminhoNome\" = 'Vida';"); // 2) Terra + Vida → Aprimorar
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6 WHERE \"SegundaEssencia\" IS NULL AND \"Elemento\" = 3 AND \"SubElemento\" = 13;"); // 1) Terra + Mundano → Invocacao
            migrationBuilder.Sql("UPDATE \"NpcAffinities\" SET \"SegundaEssencia\" = 6, \"SubElemento\" = 13 WHERE \"SegundaEssencia\" IS NULL AND \"SubElemento\" IS NULL AND \"Elemento\" = 3 AND \"CaminhoNome\" = 'Mundano';"); // 2) Terra + Mundano → Invocacao
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
