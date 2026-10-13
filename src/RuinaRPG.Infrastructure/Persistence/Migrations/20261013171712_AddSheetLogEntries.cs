using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSheetLogEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SheetLogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CharacterSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: true),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorNickname = table.Column<string>(type: "text", nullable: false),
                    ActorIsGm = table.Column<bool>(type: "boolean", nullable: false),
                    Categoria = table.Column<string>(type: "text", nullable: false),
                    Acao = table.Column<string>(type: "text", nullable: false),
                    Secao = table.Column<string>(type: "text", nullable: false),
                    Campo = table.Column<string>(type: "text", nullable: true),
                    ItemNome = table.Column<string>(type: "text", nullable: true),
                    ValorAntes = table.Column<string>(type: "text", nullable: true),
                    ValorDepois = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SheetLogEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_CharacterSheets_CharacterSheetId",
                        column: x => x.CharacterSheetId,
                        principalTable: "CharacterSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SheetLogEntries_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_CharacterSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "CharacterSheetId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_CreatureSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "CreatureSheetId", "OccurredAt" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_SheetLogEntries_NpcSheetId_OccurredAt",
                table: "SheetLogEntries",
                columns: new[] { "NpcSheetId", "OccurredAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SheetLogEntries");
        }
    }
}
