using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureExclusiveTraits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TraitId",
                table: "CreatureTraits",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "CreatureExclusiveTraitId",
                table: "CreatureTraits",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CreatureExclusiveTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false),
                    RequerEspecificacao = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureExclusiveTraits", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_CreatureExclusiveTraitId",
                table: "CreatureTraits",
                column: "CreatureExclusiveTraitId");

            migrationBuilder.AddForeignKey(
                name: "FK_CreatureTraits_CreatureExclusiveTraits_CreatureExclusiveTra~",
                table: "CreatureTraits",
                column: "CreatureExclusiveTraitId",
                principalTable: "CreatureExclusiveTraits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreatureTraits_CreatureExclusiveTraits_CreatureExclusiveTra~",
                table: "CreatureTraits");

            migrationBuilder.DropTable(
                name: "CreatureExclusiveTraits");

            migrationBuilder.DropIndex(
                name: "IX_CreatureTraits_CreatureExclusiveTraitId",
                table: "CreatureTraits");

            migrationBuilder.DropColumn(
                name: "CreatureExclusiveTraitId",
                table: "CreatureTraits");

            migrationBuilder.AlterColumn<Guid>(
                name: "TraitId",
                table: "CreatureTraits",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
