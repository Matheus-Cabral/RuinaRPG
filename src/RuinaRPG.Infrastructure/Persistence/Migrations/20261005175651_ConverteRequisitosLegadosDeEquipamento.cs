using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ConverteRequisitosLegadosDeEquipamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O jsonb escrito aqui tem a forma que System.Text.Json lê nos records de domínio: nomes de
            // propriedade como declarados e enums como número. Listas que o SQL não escreve voltam vazias
            // (o record inicializa com []).

            // 1) Requisito de Vigor (Armadura, Escudo) -> requisito estruturado "Vigor >= N".
            foreach (var coluna in new[] { "Armadura_RequisitoVigor", "RequisitoVigor" })
                migrationBuilder.Sql($"""
                    UPDATE "Items" SET "Requisitos" = jsonb_build_object('Atributos',
                        jsonb_build_array(jsonb_build_object('Atributo', {(int)Atributo.Vigor}, 'Minimo', "{coluna}")))
                    WHERE "{coluna}" IS NOT NULL;
                    """);

            // 2) Penalidade em texto (Armadura, Escudo) -> texto livre da nova penalidade, como está
            //    (ex: "-10 Reflexo"); não é convertida em penalidade estruturada de Perícia.
            foreach (var coluna in new[] { "Armadura_Penalidade", "Penalidade" })
                migrationBuilder.Sql($"""
                    UPDATE "Items" SET "PenalidadeDeRequisitos" = jsonb_build_object('Texto', btrim("{coluna}"))
                    WHERE "{coluna}" IS NOT NULL AND btrim("{coluna}") <> '';
                    """);

            // 3) Requisito de Atributo da Arma ("10 Dex" / "Dex 10") -> requisito estruturado, uma UPDATE por
            //    abreviação conhecida (as mesmas de RequisitoAtributoLegado). O padrão é ancorado e limita o número a 9 dígitos (cabe em int; acima disso vai para o passo 4), então
            //    "Dex 10 ou For 12" não casa. Limitação conhecida: um texto com pontuação, como "Dex.",
            //    também não é reconhecido e vai para o texto livre no passo 4.
            const string numero = @"\d{1,9}";
            foreach (var (abreviacao, atributo) in RequisitoAtributoLegado.Abreviacoes)
                migrationBuilder.Sql($"""
                    UPDATE "Items" SET "Requisitos" = jsonb_build_object('Atributos', jsonb_build_array(jsonb_build_object(
                        'Atributo', {(int)atributo},
                        'Minimo', (substring("RequisitoAtributo" from '{numero}'))::int)))
                    WHERE "Requisitos" IS NULL AND "RequisitoAtributo" IS NOT NULL
                      AND ("RequisitoAtributo" ~* '^\s*{numero}\s+{abreviacao}\s*$' OR "RequisitoAtributo" ~* '^\s*{abreviacao}\s+{numero}\s*$');
                    """);

            // 4) O que sobrou em texto vai para o texto livre, para o GM revisar.
            migrationBuilder.Sql("""
                UPDATE "Items" SET "PenalidadeDeRequisitos" = jsonb_build_object('Texto', 'Requisito: ' || btrim("RequisitoAtributo"))
                WHERE "Tipo" = 'Arma' AND "Requisitos" IS NULL AND "RequisitoAtributo" IS NOT NULL AND btrim("RequisitoAtributo") <> '';
                """);

            migrationBuilder.DropColumn(
                name: "Armadura_Penalidade",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Armadura_RequisitoVigor",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Penalidade",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequisitoAtributo",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "RequisitoVigor",
                table: "Items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Conversão sem volta: os valores viram jsonb em Requisitos/PenalidadeDeRequisitos.
            migrationBuilder.AddColumn<string>(
                name: "Armadura_Penalidade",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Armadura_RequisitoVigor",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Penalidade",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequisitoAtributo",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequisitoVigor",
                table: "Items",
                type: "integer",
                nullable: true);
        }
    }
}
