using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrdemToEncounterLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EncounterLogEntries_EncounterId_Sequencia",
                table: "EncounterLogEntries");

            migrationBuilder.AddColumn<long>(
                name: "Ordem",
                table: "EncounterLogEntries",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLogEntries_EncounterId_Ordem",
                table: "EncounterLogEntries",
                columns: new[] { "EncounterId", "Ordem" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EncounterLogEntries_EncounterId_Ordem",
                table: "EncounterLogEntries");

            migrationBuilder.DropColumn(
                name: "Ordem",
                table: "EncounterLogEntries");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterLogEntries_EncounterId_Sequencia",
                table: "EncounterLogEntries",
                columns: new[] { "EncounterId", "Sequencia" });
        }
    }
}
