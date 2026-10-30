using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AfinidadesPorElemento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ElementosDeAfinidade",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    Teto = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ElementosDeAfinidade", x => x.Id);
                    table.CheckConstraint("CK_ElementosDeAfinidade_Teto", "\"Teto\" IS NULL OR \"Teto\" >= 1");
                });

            migrationBuilder.CreateTable(
                name: "TotaisDeAfinidadePorCirculo",
                columns: table => new
                {
                    Circulo = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TotaisDeAfinidadePorCirculo", x => x.Circulo);
                    table.CheckConstraint("CK_TotaisDeAfinidadePorCirculo_Total", "\"Total\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "CharacterPontosDeAfinidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementoId = table.Column<int>(type: "integer", nullable: false),
                    Pontos = table.Column<int>(type: "integer", nullable: false),
                    Experiencia = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterPontosDeAfinidade", x => x.Id);
                    table.CheckConstraint("CK_CharacterPontosDeAfinidade_Pontos", "\"Pontos\" >= 0");
                    table.ForeignKey(
                        name: "FK_CharacterPontosDeAfinidade_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterPontosDeAfinidade_ElementosDeAfinidade_ElementoId",
                        column: x => x.ElementoId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CruzamentosDeAfinidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementoAId = table.Column<int>(type: "integer", nullable: false),
                    MinimoA = table.Column<int>(type: "integer", nullable: false),
                    ElementoBId = table.Column<int>(type: "integer", nullable: false),
                    MinimoB = table.Column<int>(type: "integer", nullable: false),
                    SubElementoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CruzamentosDeAfinidade", x => x.Id);
                    table.CheckConstraint("CK_CruzamentosDeAfinidade_Minimos", "\"MinimoA\" >= 1 AND \"MinimoB\" >= 1");
                    table.CheckConstraint("CK_CruzamentosDeAfinidade_ParOrdenado", "\"ElementoAId\" < \"ElementoBId\"");
                    table.ForeignKey(
                        name: "FK_CruzamentosDeAfinidade_ElementosDeAfinidade_ElementoAId",
                        column: x => x.ElementoAId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CruzamentosDeAfinidade_ElementosDeAfinidade_ElementoBId",
                        column: x => x.ElementoBId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CruzamentosDeAfinidade_ElementosDeAfinidade_SubElementoId",
                        column: x => x.SubElementoId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NpcPontosDeAfinidade",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ElementoId = table.Column<int>(type: "integer", nullable: false),
                    Pontos = table.Column<int>(type: "integer", nullable: false),
                    Experiencia = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcPontosDeAfinidade", x => x.Id);
                    table.CheckConstraint("CK_NpcPontosDeAfinidade_Pontos", "\"Pontos\" >= 0");
                    table.ForeignKey(
                        name: "FK_NpcPontosDeAfinidade_ElementosDeAfinidade_ElementoId",
                        column: x => x.ElementoId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcPontosDeAfinidade_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VisibilidadesDeElemento",
                columns: table => new
                {
                    Origem = table.Column<string>(type: "text", nullable: false),
                    Chave = table.Column<string>(type: "text", nullable: false),
                    ElementoId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VisibilidadesDeElemento", x => new { x.Origem, x.Chave, x.ElementoId });
                    table.ForeignKey(
                        name: "FK_VisibilidadesDeElemento_ElementosDeAfinidade_ElementoId",
                        column: x => x.ElementoId,
                        principalTable: "ElementosDeAfinidade",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterPontosDeAfinidade_CharacterSheetId_ElementoId",
                table: "CharacterPontosDeAfinidade",
                columns: new[] { "CharacterSheetId", "ElementoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CharacterPontosDeAfinidade_ElementoId",
                table: "CharacterPontosDeAfinidade",
                column: "ElementoId");

            migrationBuilder.CreateIndex(
                name: "IX_CruzamentosDeAfinidade_ElementoAId_ElementoBId",
                table: "CruzamentosDeAfinidade",
                columns: new[] { "ElementoAId", "ElementoBId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CruzamentosDeAfinidade_ElementoBId",
                table: "CruzamentosDeAfinidade",
                column: "ElementoBId");

            migrationBuilder.CreateIndex(
                name: "IX_CruzamentosDeAfinidade_SubElementoId",
                table: "CruzamentosDeAfinidade",
                column: "SubElementoId");

            migrationBuilder.CreateIndex(
                name: "IX_ElementosDeAfinidade_Nome",
                table: "ElementosDeAfinidade",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcPontosDeAfinidade_ElementoId",
                table: "NpcPontosDeAfinidade",
                column: "ElementoId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcPontosDeAfinidade_NpcSheetId_ElementoId",
                table: "NpcPontosDeAfinidade",
                columns: new[] { "NpcSheetId", "ElementoId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VisibilidadesDeElemento_ElementoId",
                table: "VisibilidadesDeElemento",
                column: "ElementoId");

            SemearCatalogo(migrationBuilder);
            ConverterLinhas(migrationBuilder, "CharacterAffinities", "CharacterPontosDeAfinidade", "CharacterSheetId");
            ConverterLinhas(migrationBuilder, "NpcAffinities", "NpcPontosDeAfinidade", "NpcSheetId");
            TrocarElementoDasCondicoes(migrationBuilder, paraId: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            TrocarElementoDasCondicoes(migrationBuilder, paraId: false);

            migrationBuilder.DropTable(
                name: "CharacterPontosDeAfinidade");

            migrationBuilder.DropTable(
                name: "CruzamentosDeAfinidade");

            migrationBuilder.DropTable(
                name: "NpcPontosDeAfinidade");

            migrationBuilder.DropTable(
                name: "TotaisDeAfinidadePorCirculo");

            migrationBuilder.DropTable(
                name: "VisibilidadesDeElemento");

            migrationBuilder.DropTable(
                name: "ElementosDeAfinidade");
        }

        // Os nomes do antigo enum AfinidadeElemental, na ordem dos ids do catálogo, e Mundano (18).
        private static readonly string[] NomesDoEnum =
        [
            "Terra", "Agua", "Fogo", "Ar", "Gelo", "Flora", "Ferro", "Raio", "Prever", "Alma", "Purificar", "Ecomancia",
            "Hemomancia", "Curar", "Vida", "Aprimorar", "Necromancia", "Invocacao", "Mundano",
        ];

        private static void SemearCatalogo(MigrationBuilder migrationBuilder)
        {
            string[] nomes =
            [
                "Terra", "Água", "Fogo", "Ar", "Gelo", "Flora", "Ferro", "Raio", "Prever", "Alma", "Purificar", "Ecomancia",
                "Hemomancia", "Curar", "Vida", "Aprimorar", "Necromancia", "Invocação", "Mundano",
            ];
            string Tipo(int id) => id switch { <= 3 => "Base", 9 or 14 or 18 => "Caminho", _ => "SubElemento" };
            migrationBuilder.InsertData(
                table: "ElementosDeAfinidade",
                columns: ["Id", "Nome", "Tipo", "Teto"],
                values: ToMatriz(nomes.Select((nome, id) => new object[] { id, nome, Tipo(id), null! })));
            // Os próximos elementos (criados pelo Auditor) continuam a partir do 19.
            migrationBuilder.Sql("SELECT setval(pg_get_serial_sequence('\"ElementosDeAfinidade\"', 'Id'), 18);");

            // Terra=0, Agua=1, Fogo=2, Ar=3, Alma=9, Vida=14, Mundano=18 → Sub-Elemento.
            (int A, int B, int Sub)[] cruzamentos =
            [
                (1, 3, 4), (2, 3, 7), (0, 1, 5), (0, 2, 6), (3, 9, 8), (1, 9, 10),
                (2, 14, 13), (0, 14, 15), (3, 18, 11), (1, 18, 12), (2, 18, 16), (0, 18, 17),
            ];
            migrationBuilder.Sql(
                "INSERT INTO \"CruzamentosDeAfinidade\" (\"Id\", \"ElementoAId\", \"MinimoA\", \"ElementoBId\", \"MinimoB\", \"SubElementoId\") VALUES " +
                string.Join(", ", cruzamentos.Select(c => $"(gen_random_uuid(), {c.A}, 1, {c.B}, 1, {c.Sub})")) + ";");

            // Toda Vocação mágica vê todos os Caminhos e Sub-Elementos (Auditoria de Regras R0021).
            migrationBuilder.Sql(
                "INSERT INTO \"VisibilidadesDeElemento\" (\"Origem\", \"Chave\", \"ElementoId\") " +
                "SELECT 'Vocacao', v.chave, e.\"Id\" FROM (VALUES ('Feiticeiro'), ('Adepto'), ('Bruxo')) AS v(chave) " +
                "CROSS JOIN \"ElementosDeAfinidade\" e WHERE e.\"Tipo\" <> 'Base';");
        }

        private static object[,] ToMatriz(IEnumerable<object[]> linhas)
        {
            var lista = linhas.ToList();
            var m = new object[lista.Count, lista[0].Length];
            for (var i = 0; i < lista.Count; i++)
                for (var j = 0; j < lista[i].Length; j++)
                    m[i, j] = lista[i][j];
            return m;
        }

        /// <summary>
        /// Linhas antigas → pontos por elemento, exatamente como <c>ConversaoDeLinhas.Converter</c> (Domain): cada
        /// Elemento/Caminho fica com o maior valor (nunca abaixo de 0) entre Essência 1 e 2 de todas as linhas; cada
        /// Sub-Elemento com a soma dos valores das suas linhas (nunca abaixo de 0), prevalecendo sobre uma Essência de mesmo id; a
        /// Experiência vai ao Sub-Elemento da linha ou, sem ele, à Essência 1, ficando a maior (nunca abaixo de 0);
        /// fica a linha com pontos &gt; 0 ou com Experiência. Os enums antigos mapeiam pelo nome:
        /// Elemento Ar=0 Agua=1 Fogo=2 Terra=3; EssenciaBasica Ar=0 Agua=1 Fogo=2 Terra=3 Alma=4 Vida=5 Mundano=6;
        /// SubElemento Gelo=0 Raio=1 Prever=2 Ecomancia=3 Alma=4 Flora=5 Purificar=6 Hemomancia=7 Ferro=8 Curar=9
        /// Necromancia=10 Vida=11 Aprimorar=12 Invocacao=13. Um valor fora desses enums aborta a migration (o Converter
        /// também recusa), em vez de perder a linha.
        /// </summary>
        private static void ConverterLinhas(MigrationBuilder migrationBuilder, string origem, string destino, string coluna)
        {
            migrationBuilder.Sql($$"""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM "{{origem}}"
                               WHERE "Elemento" NOT BETWEEN 0 AND 3 OR "SegundaEssencia" NOT BETWEEN 0 AND 6 OR "SubElemento" NOT BETWEEN 0 AND 13) THEN
                        RAISE EXCEPTION '{{origem}} tem um Elemento, Essência ou Sub-Elemento fora dos enums conhecidos';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql($$"""
                WITH linhas AS (
                    SELECT "{{coluna}}" AS ficha,
                           CASE "Elemento" WHEN 0 THEN 3 WHEN 1 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 0 END AS e1,
                           COALESCE("ElementoValor", 0) AS v1,
                           CASE "SegundaEssencia" WHEN 0 THEN 3 WHEN 1 THEN 1 WHEN 2 THEN 2 WHEN 3 THEN 0
                                                  WHEN 4 THEN 9 WHEN 5 THEN 14 WHEN 6 THEN 18 END AS e2,
                           COALESCE("SegundaEssenciaValor", 0) AS v2,
                           CASE "SubElemento" WHEN 0 THEN 4 WHEN 1 THEN 7 WHEN 2 THEN 8 WHEN 3 THEN 11 WHEN 4 THEN 9
                                              WHEN 5 THEN 5 WHEN 6 THEN 10 WHEN 7 THEN 12 WHEN 8 THEN 6 WHEN 9 THEN 13
                                              WHEN 10 THEN 16 WHEN 11 THEN 14 WHEN 12 THEN 15 WHEN 13 THEN 17 END AS sub,
                           COALESCE("SubElementoValor", 0) AS vs,
                           "Experiencia" AS exp
                    FROM "{{origem}}"
                ),
                essencias AS (
                    SELECT ficha, id, GREATEST(MAX(v), 0) AS v
                    FROM (SELECT ficha, e1 AS id, v1 AS v FROM linhas WHERE e1 IS NOT NULL
                          UNION ALL
                          SELECT ficha, e2, v2 FROM linhas WHERE e2 IS NOT NULL) x
                    GROUP BY ficha, id
                ),
                subs AS (
                    SELECT ficha, sub AS id, GREATEST(SUM(vs), 0) AS v FROM linhas WHERE sub IS NOT NULL GROUP BY ficha, sub
                ),
                pontos AS (
                    SELECT ficha, id, v FROM subs
                    UNION ALL
                    SELECT e.ficha, e.id, e.v FROM essencias e
                    WHERE NOT EXISTS (SELECT 1 FROM subs s WHERE s.ficha = e.ficha AND s.id = e.id)
                ),
                experiencias AS (
                    SELECT ficha, COALESCE(sub, e1) AS id, GREATEST(MAX(exp), 0) AS exp
                    FROM linhas WHERE exp IS NOT NULL AND COALESCE(sub, e1) IS NOT NULL
                    GROUP BY ficha, COALESCE(sub, e1)
                )
                INSERT INTO "{{destino}}" ("Id", "{{coluna}}", "ElementoId", "Pontos", "Experiencia")
                SELECT gen_random_uuid(), COALESCE(p.ficha, x.ficha), COALESCE(p.id, x.id), COALESCE(p.v, 0), x.exp
                FROM pontos p FULL JOIN experiencias x ON x.ficha = p.ficha AND x.id = p.id
                WHERE COALESCE(p.v, 0) > 0 OR x.exp IS NOT NULL;
                """);
        }

        /// <summary>
        /// O elemento alvo das linhas de Condição (catálogo e cópias aplicadas a participantes, jsonb) passa do nome do
        /// enum ao id do catálogo, como texto (Modelo de Dados, CondicaoModificadores.AlvoEspecifico); o Down volta.
        /// Só nas linhas de alvo Afinidade elemental ou Dano elemental; vazio (= todos) fica como está.
        /// </summary>
        private static void TrocarElementoDasCondicoes(MigrationBuilder migrationBuilder, bool paraId)
        {
            var mapa = "(VALUES " + string.Join(", ", NomesDoEnum.Select((nome, id) =>
                paraId ? $"('{nome}', '{id}')" : $"('{id}', '{nome}')")) + ") AS m(antigo, novo)";
            const string alvos = "('AfinidadeElemental', 'DanoElemental')";

            migrationBuilder.Sql($$"""
                UPDATE "CondicaoModificadores" c SET "AlvoEspecifico" = m.novo
                FROM {{mapa}}
                WHERE c."Alvo" IN {{alvos}} AND c."AlvoEspecifico" = m.antigo;
                """);

            migrationBuilder.Sql($$"""
                UPDATE "EncounterParticipantCondicoes" c SET "Modificadores" = (
                    SELECT jsonb_agg(CASE WHEN l.valor ->> 'Alvo' IN {{alvos}} AND m.novo IS NOT NULL
                                          THEN jsonb_set(l.valor, '{AlvoEspecifico}', to_jsonb(m.novo)) ELSE l.valor END
                                     ORDER BY l.ordem)
                    FROM jsonb_array_elements(c."Modificadores") WITH ORDINALITY AS l(valor, ordem)
                    LEFT JOIN {{mapa}} ON m.antigo = l.valor ->> 'AlvoEspecifico')
                WHERE EXISTS (
                    SELECT 1 FROM jsonb_array_elements(c."Modificadores") AS l(valor)
                    JOIN {{mapa}} ON m.antigo = l.valor ->> 'AlvoEspecifico'
                    WHERE l.valor ->> 'Alvo' IN {{alvos}});
                """);
        }
    }
}
