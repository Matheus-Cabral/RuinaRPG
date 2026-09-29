using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDurabilidadeMaximaDosItens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Backfill único: armas de ficha cujo item do catálogo não tinha Durabilidade Máxima
            // digitada foram gravadas com DurabilidadeAtual = 0. Agora o Máximo vem do Rank, então
            // elas passariam a aparecer "0 / N" — enchemos a atual com a durabilidade do Rank.
            // Os valores são os do seed de [[Tabela de Durabilidade por Rank]] (literais: a tabela
            // DurabilidadesPorRank só é semeada depois das migrations, então está vazia aqui).
            // S/SS (6/7) são Inquebráveis → 0. Itens com máximo digitado ficam intactos (a atual já
            // é limitada na leitura); ataques naturais de criatura (ItemId NULL) não casam o JOIN.
            foreach (var table in new[] { "CharacterWeapons", "NpcWeapons", "CreatureWeapons" })
            {
                migrationBuilder.Sql($"""
                    UPDATE "{table}" AS w
                    SET "DurabilidadeAtual" = CASE i."Arma_Rank"
                        WHEN 0 THEN 20
                        WHEN 1 THEN 45
                        WHEN 2 THEN 80
                        WHEN 3 THEN 125
                        WHEN 4 THEN 180
                        WHEN 5 THEN 245
                        ELSE 0
                    END
                    FROM "Items" AS i
                    WHERE w."ItemId" = i."Id"
                      AND i."Tipo" = 'Arma'
                      AND i."Arma_DurabilidadeMaxima" IS NULL
                      AND i."Arma_Rank" IS NOT NULL;
                    """);
            }

            migrationBuilder.DropColumn(
                name: "Arma_DurabilidadeMaxima",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Armadura_DurabilidadeMaxima",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DurabilidadeMaxima",
                table: "Items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Arma_DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Armadura_DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);
        }
    }
}
