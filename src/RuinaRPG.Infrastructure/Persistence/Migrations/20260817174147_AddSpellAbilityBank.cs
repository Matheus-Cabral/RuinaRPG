using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpellAbilityBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SpellAbilityBankEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellAbilityBankEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpellAbilityBankEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpellAbilityBankEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SpellAbilityBankEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpellAbilityBankEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpellAbilityBankEffects_SpellAbilityBankEntries_SpellAbilit~",
                        column: x => x.SpellAbilityBankEntryId,
                        principalTable: "SpellAbilityBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SpellAbilityBankEffects_SpellAbilityBankEntryId",
                table: "SpellAbilityBankEffects",
                column: "SpellAbilityBankEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_SpellAbilityBankEntries_GmId",
                table: "SpellAbilityBankEntries",
                column: "GmId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SpellAbilityBankEffects");

            migrationBuilder.DropTable(
                name: "SpellAbilityBankEntries");
        }
    }
}
