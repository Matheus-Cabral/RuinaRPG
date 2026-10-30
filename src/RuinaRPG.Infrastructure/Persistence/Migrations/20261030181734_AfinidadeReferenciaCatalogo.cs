using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AfinidadeReferenciaCatalogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_Afinidade",
                table: "NpcSheets",
                column: "Afinidade");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_Afinidade",
                table: "CreatureSheets",
                column: "Afinidade");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_Afinidade",
                table: "CharacterSheets",
                column: "Afinidade");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_ElementosDeAfinidade_Afinidade",
                table: "CharacterSheets",
                column: "Afinidade",
                principalTable: "ElementosDeAfinidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreatureSheets_ElementosDeAfinidade_Afinidade",
                table: "CreatureSheets",
                column: "Afinidade",
                principalTable: "ElementosDeAfinidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcSheets_ElementosDeAfinidade_Afinidade",
                table: "NpcSheets",
                column: "Afinidade",
                principalTable: "ElementosDeAfinidade",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_ElementosDeAfinidade_Afinidade",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_CreatureSheets_ElementosDeAfinidade_Afinidade",
                table: "CreatureSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_ElementosDeAfinidade_Afinidade",
                table: "NpcSheets");

            migrationBuilder.DropIndex(
                name: "IX_NpcSheets_Afinidade",
                table: "NpcSheets");

            migrationBuilder.DropIndex(
                name: "IX_CreatureSheets_Afinidade",
                table: "CreatureSheets");

            migrationBuilder.DropIndex(
                name: "IX_CharacterSheets_Afinidade",
                table: "CharacterSheets");
        }
    }
}
