using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTraitSpecificationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RequerEspecificacao",
                table: "Traits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Especificacao",
                table: "NpcTraits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Especificacao",
                table: "CreatureTraits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Especificacao",
                table: "CharacterTraits",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RequerEspecificacao",
                table: "Traits");

            migrationBuilder.DropColumn(
                name: "Especificacao",
                table: "NpcTraits");

            migrationBuilder.DropColumn(
                name: "Especificacao",
                table: "CreatureTraits");

            migrationBuilder.DropColumn(
                name: "Especificacao",
                table: "CharacterTraits");
        }
    }
}
