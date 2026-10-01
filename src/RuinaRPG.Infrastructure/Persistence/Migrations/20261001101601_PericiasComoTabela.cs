using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PericiasComoTabela : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "NpcSkills",
                newName: "PericiaId");

            migrationBuilder.RenameIndex(
                name: "IX_NpcSkills_NpcSheetId_Pericia",
                table: "NpcSkills",
                newName: "IX_NpcSkills_NpcSheetId_PericiaId");

            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "NpcMasteries",
                newName: "PericiaId");

            migrationBuilder.RenameColumn(
                name: "PericiaMaisTres",
                table: "Historicos",
                newName: "PericiaMaisTresId");

            migrationBuilder.RenameColumn(
                name: "PericiaMaisSeis",
                table: "Historicos",
                newName: "PericiaMaisSeisId");

            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "CreatureSkills",
                newName: "PericiaId");

            migrationBuilder.RenameIndex(
                name: "IX_CreatureSkills_CreatureSheetId_Pericia",
                table: "CreatureSkills",
                newName: "IX_CreatureSkills_CreatureSheetId_PericiaId");

            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "CreatureMasteries",
                newName: "PericiaId");

            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "CharacterSkills",
                newName: "PericiaId");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterSkills_CharacterSheetId_Pericia",
                table: "CharacterSkills",
                newName: "IX_CharacterSkills_CharacterSheetId_PericiaId");

            migrationBuilder.RenameColumn(
                name: "Pericia",
                table: "CharacterMasteries",
                newName: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSkills_PericiaId",
                table: "NpcSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcMasteries_PericiaId",
                table: "NpcMasteries",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_Historicos_PericiaMaisSeisId",
                table: "Historicos",
                column: "PericiaMaisSeisId");

            migrationBuilder.CreateIndex(
                name: "IX_Historicos_PericiaMaisTresId",
                table: "Historicos",
                column: "PericiaMaisTresId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSkills_PericiaId",
                table: "CreatureSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureMasteries_PericiaId",
                table: "CreatureMasteries",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterSkills_PericiaId",
                table: "CharacterSkills",
                column: "PericiaId");

            migrationBuilder.CreateIndex(
                name: "IX_CharacterMasteries_PericiaId",
                table: "CharacterMasteries",
                column: "PericiaId");

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterMasteries_Pericias_PericiaId",
                table: "CharacterMasteries",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CharacterSkills_Pericias_PericiaId",
                table: "CharacterSkills",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreatureMasteries_Pericias_PericiaId",
                table: "CreatureMasteries",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CreatureSkills_Pericias_PericiaId",
                table: "CreatureSkills",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Historicos_Pericias_PericiaMaisSeisId",
                table: "Historicos",
                column: "PericiaMaisSeisId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Historicos_Pericias_PericiaMaisTresId",
                table: "Historicos",
                column: "PericiaMaisTresId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcMasteries_Pericias_PericiaId",
                table: "NpcMasteries",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NpcSkills_Pericias_PericiaId",
                table: "NpcSkills",
                column: "PericiaId",
                principalTable: "Pericias",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CharacterMasteries_Pericias_PericiaId",
                table: "CharacterMasteries");

            migrationBuilder.DropForeignKey(
                name: "FK_CharacterSkills_Pericias_PericiaId",
                table: "CharacterSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_CreatureMasteries_Pericias_PericiaId",
                table: "CreatureMasteries");

            migrationBuilder.DropForeignKey(
                name: "FK_CreatureSkills_Pericias_PericiaId",
                table: "CreatureSkills");

            migrationBuilder.DropForeignKey(
                name: "FK_Historicos_Pericias_PericiaMaisSeisId",
                table: "Historicos");

            migrationBuilder.DropForeignKey(
                name: "FK_Historicos_Pericias_PericiaMaisTresId",
                table: "Historicos");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcMasteries_Pericias_PericiaId",
                table: "NpcMasteries");

            migrationBuilder.DropForeignKey(
                name: "FK_NpcSkills_Pericias_PericiaId",
                table: "NpcSkills");

            migrationBuilder.DropIndex(
                name: "IX_NpcSkills_PericiaId",
                table: "NpcSkills");

            migrationBuilder.DropIndex(
                name: "IX_NpcMasteries_PericiaId",
                table: "NpcMasteries");

            migrationBuilder.DropIndex(
                name: "IX_Historicos_PericiaMaisSeisId",
                table: "Historicos");

            migrationBuilder.DropIndex(
                name: "IX_Historicos_PericiaMaisTresId",
                table: "Historicos");

            migrationBuilder.DropIndex(
                name: "IX_CreatureSkills_PericiaId",
                table: "CreatureSkills");

            migrationBuilder.DropIndex(
                name: "IX_CreatureMasteries_PericiaId",
                table: "CreatureMasteries");

            migrationBuilder.DropIndex(
                name: "IX_CharacterSkills_PericiaId",
                table: "CharacterSkills");

            migrationBuilder.DropIndex(
                name: "IX_CharacterMasteries_PericiaId",
                table: "CharacterMasteries");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "NpcSkills",
                newName: "Pericia");

            migrationBuilder.RenameIndex(
                name: "IX_NpcSkills_NpcSheetId_PericiaId",
                table: "NpcSkills",
                newName: "IX_NpcSkills_NpcSheetId_Pericia");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "NpcMasteries",
                newName: "Pericia");

            migrationBuilder.RenameColumn(
                name: "PericiaMaisTresId",
                table: "Historicos",
                newName: "PericiaMaisTres");

            migrationBuilder.RenameColumn(
                name: "PericiaMaisSeisId",
                table: "Historicos",
                newName: "PericiaMaisSeis");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "CreatureSkills",
                newName: "Pericia");

            migrationBuilder.RenameIndex(
                name: "IX_CreatureSkills_CreatureSheetId_PericiaId",
                table: "CreatureSkills",
                newName: "IX_CreatureSkills_CreatureSheetId_Pericia");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "CreatureMasteries",
                newName: "Pericia");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "CharacterSkills",
                newName: "Pericia");

            migrationBuilder.RenameIndex(
                name: "IX_CharacterSkills_CharacterSheetId_PericiaId",
                table: "CharacterSkills",
                newName: "IX_CharacterSkills_CharacterSheetId_Pericia");

            migrationBuilder.RenameColumn(
                name: "PericiaId",
                table: "CharacterMasteries",
                newName: "Pericia");
        }
    }
}
