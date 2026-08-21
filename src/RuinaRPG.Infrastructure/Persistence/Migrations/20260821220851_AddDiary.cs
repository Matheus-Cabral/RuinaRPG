using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiaryEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CampaignId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsSecretNote = table.Column<bool>(type: "boolean", nullable: false),
                    Texto = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_AspNetUsers_AuthorUserId",
                        column: x => x.AuthorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntries_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntryImages",
                columns: table => new
                {
                    DiaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntryImages", x => new { x.DiaryEntryId, x.ImageId });
                    table.ForeignKey(
                        name: "FK_DiaryEntryImages_DiaryEntries_DiaryEntryId",
                        column: x => x.DiaryEntryId,
                        principalTable: "DiaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntryImages_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiaryEntryRecipients",
                columns: table => new
                {
                    DiaryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiaryEntryRecipients", x => new { x.DiaryEntryId, x.UserId });
                    table.ForeignKey(
                        name: "FK_DiaryEntryRecipients_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DiaryEntryRecipients_DiaryEntries_DiaryEntryId",
                        column: x => x.DiaryEntryId,
                        principalTable: "DiaryEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_AuthorUserId",
                table: "DiaryEntries",
                column: "AuthorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_CampaignId",
                table: "DiaryEntries",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntryImages_ImageId",
                table: "DiaryEntryImages",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntryRecipients_UserId",
                table: "DiaryEntryRecipients",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiaryEntryImages");

            migrationBuilder.DropTable(
                name: "DiaryEntryRecipients");

            migrationBuilder.DropTable(
                name: "DiaryEntries");
        }
    }
}
