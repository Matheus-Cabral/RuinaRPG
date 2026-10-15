using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterPhasesAndLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FinalizadoEm",
                table: "Encounters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Encounters",
                type: "text",
                nullable: false,
                defaultValue: "Preparacao");

            // Existing encounters: one that already had someone on turn is in progress; the rest never started.
            migrationBuilder.Sql("""
                UPDATE "Encounters" SET "Status" = 'EmAndamento' WHERE "CurrentParticipantId" IS NOT NULL;
                UPDATE "Encounters" SET "Status" = 'Preparacao', "CurrentRound" = 0 WHERE "CurrentParticipantId" IS NULL;
                """);

            migrationBuilder.CreateTable(
                name: "EncounterLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uuid", nullable: false),
                    Sequencia = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Turno = table.Column<int>(type: "integer", nullable: false),
                    VezParticipantId = table.Column<Guid>(type: "uuid", nullable: true),
                    VezNome = table.Column<string>(type: "text", nullable: true),
                    Tipo = table.Column<string>(type: "text", nullable: false),
                    ParticipanteNome = table.Column<string>(type: "text", nullable: true),
                    Campo = table.Column<string>(type: "text", nullable: true),
                    ValorAntes = table.Column<string>(type: "text", nullable: true),
                    ValorDepois = table.Column<string>(type: "text", nullable: true),
                    Texto = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterLogEntries_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLogEntries_EncounterId_Sequencia",
                table: "EncounterLogEntries",
                columns: new[] { "EncounterId", "Sequencia" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterLogEntries");

            migrationBuilder.DropColumn(
                name: "FinalizadoEm",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Encounters");
        }
    }
}
