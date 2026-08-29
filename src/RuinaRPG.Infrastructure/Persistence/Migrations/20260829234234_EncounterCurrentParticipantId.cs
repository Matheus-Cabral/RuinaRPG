using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EncounterCurrentParticipantId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentParticipantIndex",
                table: "Encounters");

            migrationBuilder.AddColumn<Guid>(
                name: "CurrentParticipantId",
                table: "Encounters",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_CurrentParticipantId",
                table: "Encounters",
                column: "CurrentParticipantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_EncounterParticipants_CurrentParticipantId",
                table: "Encounters",
                column: "CurrentParticipantId",
                principalTable: "EncounterParticipants",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_EncounterParticipants_CurrentParticipantId",
                table: "Encounters");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_CurrentParticipantId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "CurrentParticipantId",
                table: "Encounters");

            migrationBuilder.AddColumn<int>(
                name: "CurrentParticipantIndex",
                table: "Encounters",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
