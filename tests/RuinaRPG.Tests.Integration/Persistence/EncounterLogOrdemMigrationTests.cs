using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EncounterLogOrdemMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public EncounterLogOrdemMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_adds_Ordem_and_indexes_the_log_by_EncounterId_and_Ordem()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var colunas = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name AS \"Value\" FROM information_schema.columns WHERE table_name = 'EncounterLogEntries'").ToListAsync();
        colunas.Should().Contain("Ordem");

        var indices = await db.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE tablename = 'EncounterLogEntries'").ToListAsync();
        indices.Should().Contain("IX_EncounterLogEntries_EncounterId_Ordem");
        indices.Should().NotContain("IX_EncounterLogEntries_EncounterId_Sequencia");
    }
}
