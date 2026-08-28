using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: true),
                    CurrentRound = table.Column<int>(type: "integer", nullable: false),
                    CurrentParticipantIndex = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "EncounterParticipants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceNpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceCreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Iniciativa = table.Column<int>(type: "integer", nullable: false),
                    PVAtual = table.Column<int>(type: "integer", nullable: true),
                    PFAtual = table.Column<int>(type: "integer", nullable: true),
                    PAAtual = table.Column<int>(type: "integer", nullable: true),
                    AcoesRestantes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_CharacterSheets_SourceCharacterSheetId",
                        column: x => x.SourceCharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_CreatureSheets_SourceCreatureSheetId",
                        column: x => x.SourceCreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_EncounterParticipants_NpcSheets_SourceNpcSheetId",
                        column: x => x.SourceNpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "EncounterParticipantConditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipantConditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterParticipantConditions_EncounterParticipants_Encoun~",
                        column: x => x.EncounterParticipantId,
                        principalTable: "EncounterParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipantConditions_EncounterParticipantId",
                table: "EncounterParticipantConditions",
                column: "EncounterParticipantId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_EncounterId",
                table: "EncounterParticipants",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceCharacterSheetId",
                table: "EncounterParticipants",
                column: "SourceCharacterSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceCreatureSheetId",
                table: "EncounterParticipants",
                column: "SourceCreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipants_SourceNpcSheetId",
                table: "EncounterParticipants",
                column: "SourceNpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CampaignId",
                table: "Encounters",
                column: "CampaignId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterParticipantConditions");

            migrationBuilder.DropTable(
                name: "EncounterParticipants");

            migrationBuilder.DropTable(
                name: "Encounters");
        }
    }
}
