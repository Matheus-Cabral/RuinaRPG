using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterParticipantCondicoes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EncounterParticipantCondicoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterParticipantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CondicaoId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Icone = table.Column<string>(type: "text", nullable: false),
                    AplicadaNoTurno = table.Column<int>(type: "integer", nullable: false),
                    DuracaoTurnos = table.Column<int>(type: "integer", nullable: true),
                    Modificadores = table.Column<string>(type: "jsonb", nullable: false),
                    Desabilitacoes = table.Column<string>(type: "jsonb", nullable: false),
                    AjustesAplicados = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterParticipantCondicoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterParticipantCondicoes_EncounterParticipants_Encount~",
                        column: x => x.EncounterParticipantId,
                        principalTable: "EncounterParticipants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterParticipantCondicoes_EncounterParticipantId",
                table: "EncounterParticipantCondicoes",
                column: "EncounterParticipantId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterParticipantCondicoes");
        }
    }
}
