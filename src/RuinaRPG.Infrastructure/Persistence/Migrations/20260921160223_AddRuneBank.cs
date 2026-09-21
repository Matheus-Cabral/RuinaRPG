using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuneBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceBankEntryId",
                table: "NpcRunes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBankEntryId",
                table: "CharacterRunes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RuneBankEntryId",
                table: "CampaignAttachments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RuneBankEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RuneBankEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RuneBankEntries_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_RuneBankEntryId",
                table: "CampaignAttachments",
                column: "RuneBankEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_RuneBankEntries_GmId",
                table: "RuneBankEntries",
                column: "GmId");

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignAttachments_RuneBankEntries_RuneBankEntryId",
                table: "CampaignAttachments",
                column: "RuneBankEntryId",
                principalTable: "RuneBankEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CampaignAttachments_RuneBankEntries_RuneBankEntryId",
                table: "CampaignAttachments");

            migrationBuilder.DropTable(
                name: "RuneBankEntries");

            migrationBuilder.DropIndex(
                name: "IX_CampaignAttachments_RuneBankEntryId",
                table: "CampaignAttachments");

            migrationBuilder.DropColumn(
                name: "SourceBankEntryId",
                table: "NpcRunes");

            migrationBuilder.DropColumn(
                name: "SourceBankEntryId",
                table: "CharacterRunes");

            migrationBuilder.DropColumn(
                name: "RuneBankEntryId",
                table: "CampaignAttachments");
        }
    }
}
