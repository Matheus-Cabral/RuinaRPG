using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRuneImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "RuneBankEntries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "NpcRunes",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ImageId",
                table: "CharacterRunes",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_RuneBankEntries_ImageId",
                table: "RuneBankEntries",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRunes_ImageId",
                table: "NpcRunes",
                column: "ImageId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterRunes_ImageId",
                table: "CharacterRunes",
                column: "ImageId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterRunes_Images_ImageId",
                table: "CharacterRunes",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcRunes_Images_ImageId",
                table: "NpcRunes",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RuneBankEntries_Images_ImageId",
                table: "RuneBankEntries",
                column: "ImageId",
                principalTable: "Images",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterRunes_Images_ImageId",
                table: "CharacterRunes");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcRunes_Images_ImageId",
                table: "NpcRunes");

            migrationBuilder.DropForeignKey(
                name: "FK_RuneBankEntries_Images_ImageId",
                table: "RuneBankEntries");

            migrationBuilder.DropIndex(
                name: "IX_RuneBankEntries_ImageId",
                table: "RuneBankEntries");

            migrationBuilder.DropIndex(
                name: "IX_NpcRunes_ImageId",
                table: "NpcRunes");

            migrationBuilder.DropIndex(
                name: "IX_CharacterRunes_ImageId",
                table: "CharacterRunes");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "RuneBankEntries");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "NpcRunes");

            migrationBuilder.DropColumn(
                name: "ImageId",
                table: "CharacterRunes");
        }
    }
}
