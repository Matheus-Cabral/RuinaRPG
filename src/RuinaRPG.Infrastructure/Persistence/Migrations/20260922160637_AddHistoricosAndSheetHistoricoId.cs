using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHistoricosAndSheetHistoricoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HistoricoId",
                table: "NpcSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "HistoricoId",
                table: "CharacterSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Historicos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    PericiaMaisSeis = table.Column<int>(type: "integer", nullable: false),
                    PericiaMaisTres = table.Column<int>(type: "integer", nullable: false),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Historicos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_HistoricoId",
                table: "NpcSheets",
                column: "HistoricoId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_HistoricoId",
                table: "CharacterSheets",
                column: "HistoricoId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_Historicos_HistoricoId",
                table: "CharacterSheets",
                column: "HistoricoId",
                principalTable: "Historicos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcSheets_Historicos_HistoricoId",
                table: "NpcSheets",
                column: "HistoricoId",
                principalTable: "Historicos",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_Historicos_HistoricoId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_Historicos_HistoricoId",
                table: "NpcSheets");

            migrationBuilder.DropTable(
                name: "Historicos");

            migrationBuilder.DropIndex(
                name: "IX_NpcSheets_HistoricoId",
                table: "NpcSheets");

            migrationBuilder.DropIndex(
                name: "IX_CharacterSheets_HistoricoId",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "HistoricoId",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "HistoricoId",
                table: "CharacterSheets");
        }
    }
}
