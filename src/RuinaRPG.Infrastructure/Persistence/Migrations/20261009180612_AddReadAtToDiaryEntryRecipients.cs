using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReadAtToDiaryEntryRecipients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReadAt",
                table: "DiaryEntryRecipients",
                type: "timestamp with time zone",
                nullable: true);

            // Campanha R0015: notes that already existed before the unread counter must not show
            // up as unread on the first deploy — stamp them as read at their own creation time.
            migrationBuilder.Sql("""
                UPDATE "DiaryEntryRecipients" r
                SET "ReadAt" = d."CreatedAt"
                FROM "DiaryEntries" d
                WHERE d."Id" = r."DiaryEntryId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReadAt",
                table: "DiaryEntryRecipients");
        }
    }
}
