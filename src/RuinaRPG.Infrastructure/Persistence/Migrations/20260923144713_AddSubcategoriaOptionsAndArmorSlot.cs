using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubcategoriaOptionsAndArmorSlot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Subcategoria",
                table: "Items",
                newName: "Arma_Subcategoria");

            migrationBuilder.AddColumn<string>(
                name: "Armadura_Subcategoria",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Artefato_Subcategoria",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Escudo_Subcategoria",
                table: "Items",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArmorSlot",
                table: "EquipmentKitChoiceSlots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "SubcategoriaOptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Facet = table.Column<int>(type: "integer", nullable: false),
                    Valor = table.Column<string>(type: "text", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubcategoriaOptions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubcategoriaOptions");

            migrationBuilder.DropColumn(
                name: "Armadura_Subcategoria",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Artefato_Subcategoria",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Escudo_Subcategoria",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "ArmorSlot",
                table: "EquipmentKitChoiceSlots");

            migrationBuilder.RenameColumn(
                name: "Arma_Subcategoria",
                table: "Items",
                newName: "Subcategoria");
        }
    }
}
