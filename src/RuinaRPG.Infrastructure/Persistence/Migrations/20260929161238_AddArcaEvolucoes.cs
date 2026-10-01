using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddArcaEvolucoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArcaEvolucoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ArcaEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    CriadaEm = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArcaEvolucoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArcaEvolucoes_ArcaEntries_ArcaEntryId",
                        column: x => x.ArcaEntryId,
                        principalTable: "ArcaEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArcaEvolucoes_ArcaEntryId_Nivel",
                table: "ArcaEvolucoes",
                columns: new[] { "ArcaEntryId", "Nivel" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArcaEvolucoes");
        }
    }
}
