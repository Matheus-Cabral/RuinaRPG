using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPericias : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pericias",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false),
                    Chave = table.Column<string>(type: "text", nullable: false),
                    Nome = table.Column<string>(type: "text", nullable: false),
                    Descricao = table.Column<string>(type: "text", nullable: true),
                    AtributoSugerido = table.Column<int>(type: "integer", nullable: true),
                    DisponivelParaCriaturas = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pericias", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Pericias",
                columns: new[] { "Id", "Chave", "Nome", "Descricao", "AtributoSugerido", "DisponivelParaCriaturas", "IsDeleted" },
                values: new object[,]
                {
                    { 0, "Acrobacia", "Acrobacia", null, null, true, false },
                    { 1, "Alquimia", "Alquimia", null, null, false, false },
                    { 2, "Arcano", "Arcano", null, null, false, false },
                    { 3, "Armadilhas", "Armadilhas", null, null, false, false },
                    { 4, "ArmasBrancas", "Armas Brancas", null, null, false, false },
                    { 5, "ArtefatosMagicos", "Artefatos Mágicos", null, null, true, false },
                    { 6, "Artistico", "Artístico", null, null, false, false },
                    { 7, "Atletismo", "Atletismo", null, null, true, false },
                    { 8, "Avaliacao", "Avaliação", null, null, false, false },
                    { 9, "Biblioteca", "Biblioteca", null, null, false, false },
                    { 10, "Brigar", "Brigar", null, null, true, false },
                    { 11, "Conducao", "Condução", null, null, false, false },
                    { 12, "Conhecimentos", "Conhecimentos", null, null, false, false },
                    { 13, "Crime", "Crime", null, null, false, false },
                    { 14, "EmpatiaComAnimais", "Empatia c/ Animais", null, null, true, false },
                    { 15, "Enganacao", "Enganação", null, null, true, false },
                    { 16, "ForcaDeVontade", "Força de Vontade", null, null, true, false },
                    { 17, "Fortitude", "Fortitude", null, null, true, false },
                    { 18, "Furtividade", "Furtividade", null, null, true, false },
                    { 19, "Herborismo", "Herborismo", null, null, false, false },
                    { 20, "Intimidacao", "Intimidação", null, null, true, false },
                    { 21, "Intuicao", "Intuição", null, null, true, false },
                    { 22, "Investigacao", "Investigação", null, null, true, false },
                    { 23, "Labia", "Lábia", null, null, false, false },
                    { 24, "Lideranca", "Liderança", null, null, false, false },
                    { 25, "Linguistica", "Linguística", null, null, false, false },
                    { 26, "Medicina", "Medicina", null, null, false, false },
                    { 27, "Navegacao", "Navegação", null, null, true, false },
                    { 28, "Ocultismo", "Ocultismo", null, null, true, false },
                    { 29, "Oficio", "Ofício", null, null, false, false },
                    { 30, "Percepcao", "Percepção", null, null, true, false },
                    { 31, "Pontaria", "Pontaria", null, null, true, false },
                    { 32, "Prontidao", "Prontidão", null, null, true, false },
                    { 33, "Reflexos", "Reflexos", null, null, true, false },
                    { 34, "Religiao", "Religião", null, null, false, false },
                    { 35, "Saquear", "Saquear", null, null, false, false },
                    { 36, "Seducao", "Sedução", null, null, true, false },
                    { 37, "SensoComum", "Senso Comum", null, null, false, false },
                    { 38, "Sobrevivencia", "Sobrevivência", null, null, true, false },
                });

            migrationBuilder.CreateIndex(
                name: "IX_Pericias_Chave",
                table: "Pericias",
                column: "Chave",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Pericias_Nome",
                table: "Pericias",
                column: "Nome",
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Pericias");
        }
    }
}
