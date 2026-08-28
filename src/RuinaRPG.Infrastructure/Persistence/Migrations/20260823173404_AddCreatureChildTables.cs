using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCreatureChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatureAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureAffections_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureArmorSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureArmorSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureArmorSlots_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureArmorSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureArtifacts_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureArtifacts_Items_ArtifactItemId",
                        column: x => x.ArtifactItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    TemMaestria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureAttributes_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Pericia = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureMasteries_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureShields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureShields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureShields_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureShields_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pericia = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSkills_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceBankEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Tipo = table.Column<int>(type: "integer", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    GastoEmPI = table.Column<int>(type: "integer", nullable: false),
                    Custo = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpellAbilities_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpoils",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false),
                    DT = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpoils", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpoils_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureSpoils_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureTraits_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureWeapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true),
                    ManualNome = table.Column<string>(type: "text", nullable: true),
                    ManualTipoDeDano = table.Column<int>(type: "integer", nullable: true),
                    ManualDados = table.Column<string>(type: "text", nullable: true),
                    ManualDano = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureWeapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureWeapons_CreatureSheets_CreatureSheetId",
                        column: x => x.CreatureSheetId,
                        principalTable: "CreatureSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CreatureWeapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreatureSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatureSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreatureSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CreatureSpellAbilityEffects_CreatureSpellAbilities_Creature~",
                        column: x => x.CreatureSpellAbilityId,
                        principalTable: "CreatureSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreatureAffections_CreatureSheetId",
                table: "CreatureAffections",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArmorSlots_CreatureSheetId_Slot",
                table: "CreatureArmorSlots",
                columns: new[] { "CreatureSheetId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArmorSlots_ItemId",
                table: "CreatureArmorSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArtifacts_ArtifactItemId",
                table: "CreatureArtifacts",
                column: "ArtifactItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureArtifacts_CreatureSheetId",
                table: "CreatureArtifacts",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureAttributes_CreatureSheetId_Atributo",
                table: "CreatureAttributes",
                columns: new[] { "CreatureSheetId", "Atributo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureMasteries_CreatureSheetId",
                table: "CreatureMasteries",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureShields_CreatureSheetId",
                table: "CreatureShields",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureShields_ItemId",
                table: "CreatureShields",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSkills_CreatureSheetId_Pericia",
                table: "CreatureSkills",
                columns: new[] { "CreatureSheetId", "Pericia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpellAbilities_CreatureSheetId",
                table: "CreatureSpellAbilities",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpellAbilityEffects_CreatureSpellAbilityId",
                table: "CreatureSpellAbilityEffects",
                column: "CreatureSpellAbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpoils_CreatureSheetId",
                table: "CreatureSpoils",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureSpoils_ItemId",
                table: "CreatureSpoils",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_CreatureSheetId",
                table: "CreatureTraits",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureTraits_TraitId",
                table: "CreatureTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureWeapons_CreatureSheetId",
                table: "CreatureWeapons",
                column: "CreatureSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_CreatureWeapons_ItemId",
                table: "CreatureWeapons",
                column: "ItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreatureAffections");

            migrationBuilder.DropTable(
                name: "CreatureArmorSlots");

            migrationBuilder.DropTable(
                name: "CreatureArtifacts");

            migrationBuilder.DropTable(
                name: "CreatureAttributes");

            migrationBuilder.DropTable(
                name: "CreatureMasteries");

            migrationBuilder.DropTable(
                name: "CreatureShields");

            migrationBuilder.DropTable(
                name: "CreatureSkills");

            migrationBuilder.DropTable(
                name: "CreatureSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "CreatureSpoils");

            migrationBuilder.DropTable(
                name: "CreatureTraits");

            migrationBuilder.DropTable(
                name: "CreatureWeapons");

            migrationBuilder.DropTable(
                name: "CreatureSpellAbilities");
        }
    }
}
