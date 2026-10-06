using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEquipmentKitFixedItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "EquipmentKitItems",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "ArmorSlot",
                table: "EquipmentKitItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FixedItemId",
                table: "EquipmentKitItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BonusFixedItemId",
                table: "EquipmentKitChoiceSlots",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EquipmentKitFixedItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Dados = table.Column<string>(type: "jsonb", nullable: false),
                    Requisitos = table.Column<string>(type: "jsonb", nullable: true),
                    PenalidadeDeRequisitos = table.Column<string>(type: "jsonb", nullable: true),
                    DetalhesIncompletos = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentKitFixedItems", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitItems_FixedItemId",
                table: "EquipmentKitItems",
                column: "FixedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitChoiceSlots_BonusFixedItemId",
                table: "EquipmentKitChoiceSlots",
                column: "BonusFixedItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentKitFixedItems_Nome_Tipo",
                table: "EquipmentKitFixedItems",
                columns: new[] { "Nome", "Tipo" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentKitChoiceSlots_EquipmentKitFixedItems_BonusFixedIt~",
                table: "EquipmentKitChoiceSlots",
                column: "BonusFixedItemId",
                principalTable: "EquipmentKitFixedItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentKitItems_EquipmentKitFixedItems_FixedItemId",
                table: "EquipmentKitItems",
                column: "FixedItemId",
                principalTable: "EquipmentKitFixedItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentKitChoiceSlots_EquipmentKitFixedItems_BonusFixedIt~",
                table: "EquipmentKitChoiceSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentKitItems_EquipmentKitFixedItems_FixedItemId",
                table: "EquipmentKitItems");

            migrationBuilder.DropTable(
                name: "EquipmentKitFixedItems");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentKitItems_FixedItemId",
                table: "EquipmentKitItems");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentKitChoiceSlots_BonusFixedItemId",
                table: "EquipmentKitChoiceSlots");

            migrationBuilder.DropColumn(
                name: "ArmorSlot",
                table: "EquipmentKitItems");

            migrationBuilder.DropColumn(
                name: "FixedItemId",
                table: "EquipmentKitItems");

            migrationBuilder.DropColumn(
                name: "BonusFixedItemId",
                table: "EquipmentKitChoiceSlots");

            migrationBuilder.AlterColumn<string>(
                name: "Nome",
                table: "EquipmentKitItems",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
