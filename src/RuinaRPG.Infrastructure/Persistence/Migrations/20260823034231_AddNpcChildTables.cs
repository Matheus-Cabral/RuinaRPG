using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddNpcChildTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NpcAffections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Favorabilidade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAffections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAffections_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAffinities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Elemento = table.Column<int>(type: "integer", nullable: false),
                    SubElemento = table.Column<int>(type: "integer", nullable: false),
                    CaminhoNome = table.Column<string>(type: "text", nullable: false),
                    Experiencia = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAffinities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAffinities_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcArmorSlots",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Slot = table.Column<int>(type: "integer", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcArmorSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcArmorSlots_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcArmorSlots_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcArtifacts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ArtifactItemId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcArtifacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcArtifacts_Items_ArtifactItemId",
                        column: x => x.ArtifactItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcArtifacts_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcAttributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false),
                    Bonus = table.Column<int>(type: "integer", nullable: false),
                    TemMaestria = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcAttributes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcAttributes_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcInventoryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Qtd = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcInventoryItems_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcInventoryItems_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcMasteries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Pericia = table.Column<int>(type: "integer", nullable: false),
                    Atributo = table.Column<int>(type: "integer", nullable: false),
                    GastoMaestria = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcMasteries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcMasteries_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcRunes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcRunes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcRunes_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcShields",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcShields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcShields_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcShields_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSkills",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Pericia = table.Column<int>(type: "integer", nullable: false),
                    Gasto = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSkills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSkills_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSpellAbilities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.PrimaryKey("PK_NpcSpellAbilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSpellAbilities_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcTraits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    TraitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Polaridade = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcTraits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcTraits_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NpcTraits_Traits_TraitId",
                        column: x => x.TraitId,
                        principalTable: "Traits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NpcWeapons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSheetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEquipped = table.Column<bool>(type: "boolean", nullable: false),
                    DurabilidadeAtual = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcWeapons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcWeapons_Items_ItemId",
                        column: x => x.ItemId,
                        principalTable: "Items",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NpcWeapons_NpcSheets_NpcSheetId",
                        column: x => x.NpcSheetId,
                        principalTable: "NpcSheets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "NpcSpellAbilityEffects",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    NpcSpellAbilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    EfeitoNome = table.Column<string>(type: "text", nullable: false),
                    Quantidade = table.Column<int>(type: "integer", nullable: true),
                    CustoPI = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NpcSpellAbilityEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NpcSpellAbilityEffects_NpcSpellAbilities_NpcSpellAbilityId",
                        column: x => x.NpcSpellAbilityId,
                        principalTable: "NpcSpellAbilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NpcAffections_NpcSheetId",
                table: "NpcAffections",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAffinities_NpcSheetId",
                table: "NpcAffinities",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArmorSlots_ItemId",
                table: "NpcArmorSlots",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArmorSlots_NpcSheetId_Slot",
                table: "NpcArmorSlots",
                columns: new[] { "NpcSheetId", "Slot" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcArtifacts_ArtifactItemId",
                table: "NpcArtifacts",
                column: "ArtifactItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcArtifacts_NpcSheetId",
                table: "NpcArtifacts",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcAttributes_NpcSheetId_Atributo",
                table: "NpcAttributes",
                columns: new[] { "NpcSheetId", "Atributo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcInventoryItems_ItemId",
                table: "NpcInventoryItems",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcInventoryItems_NpcSheetId",
                table: "NpcInventoryItems",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcMasteries_NpcSheetId",
                table: "NpcMasteries",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcRunes_NpcSheetId",
                table: "NpcRunes",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcShields_ItemId",
                table: "NpcShields",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcShields_NpcSheetId",
                table: "NpcShields",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSkills_NpcSheetId_Pericia",
                table: "NpcSkills",
                columns: new[] { "NpcSheetId", "Pericia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NpcSpellAbilities_NpcSheetId",
                table: "NpcSpellAbilities",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcSpellAbilityEffects_NpcSpellAbilityId",
                table: "NpcSpellAbilityEffects",
                column: "NpcSpellAbilityId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcTraits_NpcSheetId",
                table: "NpcTraits",
                column: "NpcSheetId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcTraits_TraitId",
                table: "NpcTraits",
                column: "TraitId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcWeapons_ItemId",
                table: "NpcWeapons",
                column: "ItemId");

            migrationBuilder.CreateIndex(
                name: "IX_NpcWeapons_NpcSheetId",
                table: "NpcWeapons",
                column: "NpcSheetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NpcAffections");

            migrationBuilder.DropTable(
                name: "NpcAffinities");

            migrationBuilder.DropTable(
                name: "NpcArmorSlots");

            migrationBuilder.DropTable(
                name: "NpcArtifacts");

            migrationBuilder.DropTable(
                name: "NpcAttributes");

            migrationBuilder.DropTable(
                name: "NpcInventoryItems");

            migrationBuilder.DropTable(
                name: "NpcMasteries");

            migrationBuilder.DropTable(
                name: "NpcRunes");

            migrationBuilder.DropTable(
                name: "NpcShields");

            migrationBuilder.DropTable(
                name: "NpcSkills");

            migrationBuilder.DropTable(
                name: "NpcSpellAbilityEffects");

            migrationBuilder.DropTable(
                name: "NpcTraits");

            migrationBuilder.DropTable(
                name: "NpcWeapons");

            migrationBuilder.DropTable(
                name: "NpcSpellAbilities");
        }
    }
}
