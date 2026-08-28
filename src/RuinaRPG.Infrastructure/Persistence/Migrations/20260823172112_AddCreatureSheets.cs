using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatureSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Raca = table.Column<string>(type: "text", nullable: true),
                    Arquetipo = table.Column<int>(type: "integer", nullable: true),
                    SubArquetipo = table.Column<string>(type: "text", nullable: true),
                    Afinidade = table.Column<int>(type: "integer", nullable: true),
                    Propriedade = table.Column<string>(type: "text", nullable: true),
                    Rank = table.Column<int>(type: "integer", nullable: true),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    ExperienciaAtual = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicao = table.Column<int>(type: "integer", nullable: false),
                    VitalidadeAtual = table.Column<int>(type: "integer", nullable: false),
                    FocoAtual = table.Column<int>(type: "integer", nullable: false),
                    AdrenalinaAtual = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<int>(type: "integer", nullable: false),
                    LastDismissedLevelUpLevel = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CreatureSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_GmId",
                table: "CreatureSheets",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_ImageId",
                table: "CreatureSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSheets_OwnerId",
                table: "CreatureSheets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatureSheets");
        }
    }
}
