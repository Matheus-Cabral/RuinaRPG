using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAffinityElementoAndSubElementoValor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ElementoValor",
                table: "NpcAffinities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubElementoValor",
                table: "NpcAffinities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ElementoValor",
                table: "CharacterAffinities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SubElementoValor",
                table: "CharacterAffinities",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ElementoValor",
                table: "NpcAffinities");

            migrationBuilder.DropColumn(
                name: "SubElementoValor",
                table: "NpcAffinities");

            migrationBuilder.DropColumn(
                name: "ElementoValor",
                table: "CharacterAffinities");

            migrationBuilder.DropColumn(
                name: "SubElementoValor",
                table: "CharacterAffinities");
        }
    }
}
