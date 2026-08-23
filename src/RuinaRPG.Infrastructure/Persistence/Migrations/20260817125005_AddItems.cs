using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddItems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GmId = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Peso = table.Column<decimal>(type: "numeric", nullable: false),
                    Preco = table.Column<int>(type: "integer", nullable: false),
                    ImageId = table.Column<Guid>(type: "uuid", nullable: true),
                    Tipo = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: false),
                    Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Tier = table.Column<int>(type: "integer", nullable: true),
                    Empunhadura = table.Column<int>(type: "integer", nullable: true),
                    Dados = table.Column<string>(type: "text", nullable: true),
                    Dano = table.Column<int>(type: "integer", nullable: true),
                    Critico = table.Column<string>(type: "text", nullable: true),
                    Alcance = table.Column<int>(type: "integer", nullable: true),
                    TipoDeDano = table.Column<int>(type: "integer", nullable: true),
                    RequisitoAtributo = table.Column<string>(type: "text", nullable: true),
                    Arma_DurabilidadeMaxima = table.Column<int>(type: "integer", nullable: true),
                    Armadura_Categoria = table.Column<int>(type: "integer", nullable: true),
                    Defesa = table.Column<int>(type: "integer", nullable: true),
                    RF = table.Column<int>(type: "integer", nullable: true),
                    RM = table.Column<int>(type: "integer", nullable: true),
                    Armadura_Penalidade = table.Column<string>(type: "text", nullable: true),
                    Armadura_RequisitoVigor = table.Column<int>(type: "integer", nullable: true),
                    Armadura_DurabilidadeMaxima = table.Column<int>(type: "integer", nullable: true),
                    TipoDeAlvo = table.Column<int>(type: "integer", nullable: true),
                    Alvo = table.Column<string>(type: "text", nullable: true),
                    Valor = table.Column<int>(type: "integer", nullable: true),
                    Categoria = table.Column<int>(type: "integer", nullable: true),
                    BonusDefesa = table.Column<int>(type: "integer", nullable: true),
                    Penalidade = table.Column<string>(type: "text", nullable: true),
                    RequisitoVigor = table.Column<int>(type: "integer", nullable: true),
                    DurabilidadeMaxima = table.Column<int>(type: "integer", nullable: true),
                    ItemGeral_Subcategoria = table.Column<string>(type: "text", nullable: true),
                    Descricao = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Items_AspNetUsers_GmId",
                        column: x => x.GmId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Items_Images_ImageId",
                        column: x => x.ImageId,
                        principalTable: "Images",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Items_GmId",
                table: "Items",
                column: "GmId");

            migrationBuilder.CreateIndex(
                name: "IX_Items_ImageId",
                table: "Items",
                column: "ImageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Items");
        }
    }
}
