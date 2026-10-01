using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class PericiasMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;
    public PericiasMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_seeds_the_39_pericias_with_the_legacy_ids_keys_and_names()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var linhas = await db.Pericias.OrderBy(p => p.Id).ToListAsync();

        linhas.Select(p => (p.Id, p.Chave, p.Nome, p.DisponivelParaCriaturas))
            .Should().Equal(PericiasIniciais.Todas.Select(p => (p.Id, p.Chave, p.Nome, p.DisponivelParaCriaturas)));
        linhas.Should().OnlyContain(p => !p.IsDeleted && p.Descricao == null && p.AtributoSugerido == null);
    }
}
