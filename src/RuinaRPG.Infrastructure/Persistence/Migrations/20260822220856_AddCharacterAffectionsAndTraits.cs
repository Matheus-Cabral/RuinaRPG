using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterAffectionsAndTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterAffections_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterAffections_CharacterSheetId",
                table: "CharacterAffections",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_CharacterSheetId",
                table: "CharacterTraits",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterTraits_TraitId",
                table: "CharacterTraits",
                column: "TraitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterAffections");

            migrationBuilder.DropTable(
                name: "CharacterTraits");
        }
    }
}
