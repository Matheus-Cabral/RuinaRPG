using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCharacterSheets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CharacterSheets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_CharacterSheets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_AspNetUsers_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CharacterSheets_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_CampaignId",
                table: "CharacterSheets",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_ImageId",
                table: "CharacterSheets",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSheets_OwnerId",
                table: "CharacterSheets",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CharacterSheets");
        }
    }
}
