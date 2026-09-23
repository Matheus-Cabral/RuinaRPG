using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentKitsAndSheetEquipmentKitId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentKitId",
                table: "NpcSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EquipmentKitId",
                table: "CharacterSheets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentKits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Ciclos = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKitChoiceSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Label = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    SubcategoriasCsv = table.Column<string>(type: "text", nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    BonusSubcategoria = table.Column<string>(type: "text", nullable: true),
                    BonusNome = table.Column<string>(type: "text", nullable: true),
                    BonusQtd = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitChoiceSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentKitChoiceSlots_EquipmentKits_KitId",
                        column: x => x.KitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentKitItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    SubcategoriaHint = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentKitItems_EquipmentKits_KitId",
                        column: x => x.KitId,
                        principalTable: "EquipmentKits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_EquipmentKitId",
                table: "NpcSheets",
                column: "EquipmentKitId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_EquipmentKitId",
                table: "CharacterSheets",
                column: "EquipmentKitId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitChoiceSlots_KitId",
                table: "EquipmentKitChoiceSlots",
                column: "KitId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitItems_KitId",
                table: "EquipmentKitItems",
                column: "KitId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSheets_EquipmentKits_EquipmentKitId",
                table: "CharacterSheets",
                column: "EquipmentKitId",
                principalTable: "EquipmentKits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcSheets_EquipmentKits_EquipmentKitId",
                table: "NpcSheets",
                column: "EquipmentKitId",
                principalTable: "EquipmentKits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSheets_EquipmentKits_EquipmentKitId",
                table: "CharacterSheets");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSheets_EquipmentKits_EquipmentKitId",
                table: "NpcSheets");

            migrationBuilder.DropTable(
                name: "EquipmentKitChoiceSlots");

            migrationBuilder.DropTable(
                name: "EquipmentKitItems");

            migrationBuilder.DropTable(
                name: "EquipmentKits");

            migrationBuilder.DropIndex(
                name: "IX_NpcSheets_EquipmentKitId",
                table: "NpcSheets");

            migrationBuilder.DropIndex(
                name: "IX_CharacterSheets_EquipmentKitId",
                table: "CharacterSheets");

            migrationBuilder.DropColumn(
                name: "EquipmentKitId",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "EquipmentKitId",
                table: "CharacterSheets");
        }
    }
}
