using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRequisitosDeEquipamento : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PenalidadeDeRequisitos",
                table: "Items",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Requisitos",
                table: "Items",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PenalidadeDeRequisitos",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "Requisitos",
                table: "Items");
        }
    }
}
