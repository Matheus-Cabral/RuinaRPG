using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RuinaRPG.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterRowVersion : Migration
    {
        /// <inheritdoc />
        // Intencionalmente vazia: "xmin" é a coluna de sistema do PostgreSQL, presente em toda tabela.
        // O EF gera AddColumn para ela; criá-la de verdade falharia. O snapshot registra o token.
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
