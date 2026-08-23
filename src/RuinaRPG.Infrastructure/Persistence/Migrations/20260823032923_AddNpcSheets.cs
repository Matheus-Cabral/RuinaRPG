using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NpcSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    Linhagem = table.Column<int>(type: "integer", nullable: true),
                    Variante = table.Column<int>(type: "integer", nullable: true),
                    Vocacao = table.Column<int>(type: "integer", nullable: true),
                    SubVocacao = table.Column<string>(type: "text", nullable: true),
                    Afinidade = table.Column<int>(type: "integer", nullable: true),
                    Propriedade = table.Column<string>(type: "text", nullable: true),
                    Nivel = table.Column<int>(type: "integer", nullable: false),
                    Circulo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    PossuiCoracaoDeMana = table.Column<bool>(type: "boolean", nullable: false),
                    ExperienciaAtual = table.Column<int>(type: "integer", nullable: false),
                    EAPAtual = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankF = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankE = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankD = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankC = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankB = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankA = table.Column<int>(type: "integer", nullable: false),
                    NucleosRankS = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoAtual = table.Column<int>(type: "integer", nullable: false),
                    PontosDeIgnicaoTotal = table.Column<int>(type: "integer", nullable: false),
                    VitalidadeAtual = table.Column<int>(type: "integer", nullable: false),
                    FocoAtual = table.Column<int>(type: "integer", nullable: false),
                    AdrenalinaAtual = table.Column<int>(type: "integer", nullable: false),
                    EstresseAtual = table.Column<int>(type: "integer", nullable: false),
                    Cobertura = table.Column<int>(type: "integer", nullable: false),
                    Ciclos = table.Column<int>(type: "integer", nullable: false),
                    LastDismissedLevelUpLevel = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSheets_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NpcSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_GmId",
                table: "NpcSheets",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_ImageId",
                table: "NpcSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSheets_OwnerId",
                table: "NpcSheets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpcSheets");
        }
    }
}
