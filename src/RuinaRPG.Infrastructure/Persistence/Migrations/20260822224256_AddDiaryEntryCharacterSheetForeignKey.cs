using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDiaryEntryCharacterSheetForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DiaryEntries_CharacterSheetId",
                table: "DiaryEntries",
                column: "CharacterSheetId");

            migrationBuilder.AddForeignKey(
                name: "FK_DiaryEntries_CharacterSheets_CharacterSheetId",
                table: "DiaryEntries",
                column: "CharacterSheetId",
                principalTable: "CharacterSheets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DiaryEntries_CharacterSheets_CharacterSheetId",
                table: "DiaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_DiaryEntries_CharacterSheetId",
                table: "DiaryEntries");
        }
    }
}
