using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRacialAbilityOverrideAndArcaEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArcaRolada",
                table: "NpcSheets",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ArcaRolada",
                table: "CharacterSheets",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArcaEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Roll = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcaEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArcaEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RacialAbilityOverrides",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Variante = table.Column<int>(type: "integer", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RacialAbilityOverrides", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RacialAbilityOverrides_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcaEntries_GmId_Roll",
                table: "ArcaEntries",
                columns: new[] { "GmId", "Roll" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RacialAbilityOverrides_GmId_Variante",
                table: "RacialAbilityOverrides",
                columns: new[] { "GmId", "Variante" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcaEntries");

            migrationBuilder.DropTable(
                name: "RacialAbilityOverrides");

            migrationBuilder.DropColumn(
                name: "ArcaRolada",
                table: "NpcSheets");

            migrationBuilder.DropColumn(
                name: "ArcaRolada",
                table: "CharacterSheets");
        }
    }
}
