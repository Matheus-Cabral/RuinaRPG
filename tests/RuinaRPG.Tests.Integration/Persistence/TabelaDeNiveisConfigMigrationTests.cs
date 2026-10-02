using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules.Niveis;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TabelaDeNiveisConfigMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TabelaDeNiveisConfigMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_single_row_config_table_empty_and_persists_the_flag()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddTabelaDeNiveisConfig"));
        (await db.TabelaDeNiveisConfigs.CountAsync()).Should().Be(0);

        db.TabelaDeNiveisConfigs.Add(new TabelaDeNiveisConfig { MostrarLimitesNoLivro = true });
        await db.SaveChangesAsync();

        var config = await db.TabelaDeNiveisConfigs.SingleAsync();
        config.Id.Should().Be(1);
        config.MostrarLimitesNoLivro.Should().BeTrue();

        await using var outro = new RuinaRpgDbContext(options);
        outro.TabelaDeNiveisConfigs.Add(new TabelaDeNiveisConfig());
        var act = async () => await outro.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>(); // Id fixo em 1: só existe uma linha
    }
}
