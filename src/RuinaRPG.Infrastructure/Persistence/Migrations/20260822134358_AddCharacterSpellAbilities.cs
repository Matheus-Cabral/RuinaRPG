using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSpellAbilities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpellAbilities_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CharacterSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CharacterSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSpellAbilityEffects_CharacterSpellAbilities_Charac~",
                        column: x => x.CharacterSpellAbilityId,
                        principalTable: "CharacterSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpellAbilities_CharacterSheetId",
                table: "CharacterSpellAbilities",
                column: "CharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSpellAbilityEffects_CharacterSpellAbilityId",
                table: "CharacterSpellAbilityEffects",
                column: "CharacterSpellAbilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "CharacterSpellAbilities");
        }
    }
}
