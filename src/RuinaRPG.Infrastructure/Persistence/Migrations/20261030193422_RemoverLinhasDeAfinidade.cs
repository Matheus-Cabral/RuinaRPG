using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// 2.2.0: tira as linhas antigas de Afinidade (CharacterAffinities, NpcAffinities), já convertidas em pontos por
    /// elemento pela AfinidadesPorElemento. O Down recria as tabelas vazias: a conversão não tem volta exata
    /// (deploy/data-changes.yml lista a perda).
    /// </summary>
    public partial class RemoverLinhasDeAfinidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterAffinities");

            migrationBuilder.DropTable(
                name: "NpcAffinities");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterAffinities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Elemento = table.Column<int>(type: "integer", nullable: true),
                    ElementoValor = table.Column<int>(type: "integer", nullable: true),
                    Experiencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssenciaValor = table.Column<int>(type: "integer", nullable: true),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubElemento = table.Column<int>(type: "integer", nullable: true),
                    SubElementoValor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAffinities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAffinities_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAffinities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Elemento = table.Column<int>(type: "integer", nullable: true),
                    ElementoValor = table.Column<int>(type: "integer", nullable: true),
                    Experiencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssencia = table.Column<int>(type: "integer", nullable: true),
                    SegundaEssenciaValor = table.Column<int>(type: "integer", nullable: true),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubElemento = table.Column<int>(type: "integer", nullable: true),
                    SubElementoValor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAffinities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAffinities_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAffinities_CharacterSheetId",
                table: "CharacterAffinities",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAffinities_NpcSheetId",
                table: "NpcAffinities",
                column: "NpcSheetId");
        }
    }
}
