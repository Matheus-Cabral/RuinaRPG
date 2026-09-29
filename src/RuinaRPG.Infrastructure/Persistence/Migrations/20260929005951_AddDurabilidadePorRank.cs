using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDurabilidadePorRank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // The pre-migration "Tier" column on Items only ever held data for Arma (Armadura and
            // Escudo had no Tier/Rank column at all yet) — RenameColumn (not drop+add) is what
            // preserves that data, and it must land on Arma_Rank specifically, not on whichever
            // sibling EF's migration scaffolding happens to pick.
            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "Items",
                newName: "Arma_Rank");

            migrationBuilder.RenameColumn(
                name: "Tier",
                table: "EquipmentKitChoiceSlots",
                newName: "Rank");

            migrationBuilder.AddColumn<int>(
                name: "Armadura_Rank",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Escudo_Rank",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DurabilidadesPorRank",
                columns: table => new
                {
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    Durabilidade = table.Column<int>(type: "integer", nullable: true),
                    Inquebravel = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DurabilidadesPorRank", x => x.Rank);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DurabilidadesPorRank");

            migrationBuilder.DropColumn(
                name: "Armadura_Rank",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Escudo_Rank",
                table: "Items");

            migrationBuilder.RenameColumn(
                name: "Arma_Rank",
                table: "Items",
                newName: "Tier");

            migrationBuilder.RenameColumn(
                name: "Rank",
                table: "EquipmentKitChoiceSlots",
                newName: "Tier");
        }
    }
}
