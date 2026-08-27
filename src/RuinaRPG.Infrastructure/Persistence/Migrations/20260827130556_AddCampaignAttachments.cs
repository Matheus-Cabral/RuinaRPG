using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CampaignAttachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    SpellAbilityBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPublic = table.Column<bool>(type: "boolean", nullable: false),
                    NpcNomePublico = table.Column<bool>(type: "boolean", nullable: false),
                    NpcImagemPublica = table.Column<bool>(type: "boolean", nullable: false),
                    CreatureNomePublico = table.Column<bool>(type: "boolean", nullable: false),
                    CreatureImagemPublica = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CampaignAttachments_SpellAbilityBankEntries_SpellAbilityBan~",
                        column: x => x.SpellAbilityBankEntryId,
                        principalTable: "SpellAbilityBankEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_CampaignId",
                table: "CampaignAttachments",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_CreatureSheetId",
                table: "CampaignAttachments",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_ImageId",
                table: "CampaignAttachments",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_ItemId",
                table: "CampaignAttachments",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_NpcSheetId",
                table: "CampaignAttachments",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAttachments_SpellAbilityBankEntryId",
                table: "CampaignAttachments",
                column: "SpellAbilityBankEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CampaignAttachments");
        }
    }
}
