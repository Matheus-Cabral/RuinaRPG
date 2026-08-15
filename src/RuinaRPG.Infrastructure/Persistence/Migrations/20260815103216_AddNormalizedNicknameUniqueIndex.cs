using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNormalizedNicknameUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NormalizedNickname",
                table: "AspNetUsers",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill existing rows before the unique index goes on, otherwise every
            // pre-existing user would collide on the "" default. If this migration then
            // fails on the index, the database already holds nicknames that differ only by
            // case and they must be reconciled by hand before it can be applied.
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "NormalizedNickname" = UPPER("Nickname");""");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_NormalizedNickname",
                table: "AspNetUsers",
                column: "NormalizedNickname",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_NormalizedNickname",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "NormalizedNickname",
                table: "AspNetUsers");
        }
    }
}
