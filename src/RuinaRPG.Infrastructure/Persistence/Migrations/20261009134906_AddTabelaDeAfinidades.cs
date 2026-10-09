using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTabelaDeAfinidades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TabelaDeAfinidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Afinidade = table.Column<int>(type: "integer", nullable: false),
                    Eficiencia = table.Column<int>(type: "integer", nullable: false),
                    Dano = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TabelaDeAfinidades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TabelaDeAfinidades_Afinidade",
                table: "TabelaDeAfinidades",
                column: "Afinidade",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TabelaDeAfinidades");
        }
    }
}
