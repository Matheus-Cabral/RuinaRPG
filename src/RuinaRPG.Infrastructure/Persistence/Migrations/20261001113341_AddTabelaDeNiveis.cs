using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTabelaDeNiveis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ColunasDeNivel",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    ChaveDeSistema = table.Column<string>(type: "text", nullable: true),
                    Ordem = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ColunasDeNivel", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "NiveisProgressao",
                columns: table => new
                {
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    OutrosBonus = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NiveisProgressao", x => x.Nivel);
                });

            migrationBuilder.CreateTable(
                name: "ValoresDeNivel",
                columns: table => new
                {
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    ColunaId = table.Column<Guid>(type: "uuid", nullable: false),
                    Valor = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresDeNivel", x => new { x.Nivel, x.ColunaId });
                    table.ForeignKey(
                        name: "FK_ValoresDeNivel_ColunasDeNivel_ColunaId",
                        column: x => x.ColunaId,
                        principalTable: "ColunasDeNivel",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ValoresDeNivel_NiveisProgressao_Nivel",
                        column: x => x.Nivel,
                        principalTable: "NiveisProgressao",
                        principalColumn: "Nivel",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ColunasDeNivel_ChaveDeSistema",
                table: "ColunasDeNivel",
                column: "ChaveDeSistema",
                unique: true,
                filter: "\"ChaveDeSistema\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ValoresDeNivel_ColunaId",
                table: "ValoresDeNivel",
                column: "ColunaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValoresDeNivel");

            migrationBuilder.DropTable(
                name: "ColunasDeNivel");

            migrationBuilder.DropTable(
                name: "NiveisProgressao");
        }
    }
}
