using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterRunesAndMasteries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Pericia = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterMasteries_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterRunes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterRunes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterRunes_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterMasteries_CharacterSheetId",
                table: "CharacterMasteries",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRunes_CharacterSheetId",
                table: "CharacterRunes",
                column: "CharacterSheetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterMasteries");

            migrationBuilder.DropTable(
                name: "CharacterRunes");
        }
    }
}
