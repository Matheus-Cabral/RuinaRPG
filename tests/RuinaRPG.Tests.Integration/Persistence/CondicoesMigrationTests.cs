using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CondicoesMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CondicoesMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_three_Condicao_tables_with_a_unique_Nome_among_non_deleted_rows()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var tabelas = await db.Database.SqlQueryRaw<string>(
            "SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_name IN ('Condicoes', 'CondicaoModificadores', 'CondicaoDesabilitacoes')").ToListAsync();
        tabelas.Should().BeEquivalentTo("Condicoes", "CondicaoModificadores", "CondicaoDesabilitacoes");

        var indice = await db.Database.SqlQueryRaw<string>(
            "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'IX_Condicoes_Nome'").SingleAsync();
        indice.Should().Contain("UNIQUE").And.Contain("IsDeleted");

        var cascatas = await db.Database.SqlQueryRaw<string>(
            "SELECT constraint_name AS \"Value\" FROM information_schema.referential_constraints WHERE delete_rule = 'CASCADE' AND constraint_name LIKE 'FK_Condicao%'").ToListAsync();
        cascatas.Should().HaveCount(2);
    }
}
