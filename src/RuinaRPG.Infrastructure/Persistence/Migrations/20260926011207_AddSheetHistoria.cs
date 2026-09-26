using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetHistoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Historia",
                table: "NpcSheets",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Historia",
                table: "CharacterSheets",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Historia",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "Historia",
                table: "CharacterSheets");
        }
    }
}
