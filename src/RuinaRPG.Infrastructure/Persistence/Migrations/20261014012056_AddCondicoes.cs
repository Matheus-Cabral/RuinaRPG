using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCondicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Condicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Icone = table.Column<string>(type: "text", nullable: false),
                    DuracaoPadraoTurnos = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Condicoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CondicaoDesabilitacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicaoDesabilitacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicaoDesabilitacoes_Condicoes_CondicaoId",
                        column: x => x.CondicaoId,
                        principalTable: "Condicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CondicaoModificadores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: false),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    Alvo = table.Column<string>(type: "text", nullable: false),
                    AlvoEspecifico = table.Column<string>(type: "text", nullable: true),
                    TipoDeValor = table.Column<string>(type: "text", nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: false),
                    Momento = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CondicaoModificadores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CondicaoModificadores_Condicoes_CondicaoId",
                        column: x => x.CondicaoId,
                        principalTable: "Condicoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CondicaoDesabilitacoes_CondicaoId",
                table: "CondicaoDesabilitacoes",
                column: "CondicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_CondicaoModificadores_CondicaoId",
                table: "CondicaoModificadores",
                column: "CondicaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Condicoes_Nome",
                table: "Condicoes",
                column: "Nome",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CondicaoDesabilitacoes");

            migrationBuilder.DropTable(
                name: "CondicaoModificadores");

            migrationBuilder.DropTable(
                name: "Condicoes");
        }
    }
}
