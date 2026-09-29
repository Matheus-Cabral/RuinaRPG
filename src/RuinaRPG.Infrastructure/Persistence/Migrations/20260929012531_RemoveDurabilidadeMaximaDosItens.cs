using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RemoveDurabilidadeMaximaDosItens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Arma_DurabilidadeMaxima",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Armadura_DurabilidadeMaxima",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "DurabilidadeMaxima",
                table: "Items");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Arma_DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Armadura_DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurabilidadeMaxima",
                table: "Items",
                type: "integer",
                nullable: true);
        }
    }
}
