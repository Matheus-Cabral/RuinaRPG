using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEfeitos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Efeitos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Grau = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: false),
                    TipoDeCusto = table.Column<int>(type: "integer", nullable: false),
                    CustoFixo = table.Column<int>(type: "integer", nullable: true),
                    CustoPorUnidade = table.Column<int>(type: "integer", nullable: true),
                    UnidadeLabel = table.Column<string>(type: "text", nullable: true),
                    QuantidadeDerivadaDeEfeito = table.Column<string>(type: "text", nullable: true),
                    MaxUnidades = table.Column<int>(type: "integer", nullable: true),
                    MaxEscalaPorGrau = table.Column<bool>(type: "boolean", nullable: false),
                    MaxContandoAPartirDoGrau = table.Column<int>(type: "integer", nullable: true),
                    CustoAlternativo = table.Column<int>(type: "integer", nullable: true),
                    CustoAlternativoAPartirDoGrau = table.Column<int>(type: "integer", nullable: true),
                    PreRequisitosJson = table.Column<string>(type: "text", nullable: true),
                    IsCustomized = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Efeitos", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Efeitos");
        }
    }
}
