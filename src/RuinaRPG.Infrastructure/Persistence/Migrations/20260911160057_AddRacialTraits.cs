using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRacialTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsRacial",
                table: "NpcTraits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RacialVariante",
                table: "NpcTraits",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsRacial",
                table: "CharacterTraits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "RacialVariante",
                table: "CharacterTraits",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RacialTraitOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variante = table.Column<int>(type: "integer", nullable: false),
                    GratuitaOptionsJson = table.Column<string>(type: "text", nullable: false),
                    ObrigatoriaOptionsJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacialTraitOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacialTraitOverrides_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RacialTraitOverrides_GmId_Variante",
                table: "RacialTraitOverrides",
                columns: new[] { "GmId", "Variante" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RacialTraitOverrides");

            migrationBuilder.DropColumn(
                name: "IsRacial",
                table: "NpcTraits");

            migrationBuilder.DropColumn(
                name: "RacialVariante",
                table: "NpcTraits");

            migrationBuilder.DropColumn(
                name: "IsRacial",
                table: "CharacterTraits");

            migrationBuilder.DropColumn(
                name: "RacialVariante",
                table: "CharacterTraits");
        }
    }
}
