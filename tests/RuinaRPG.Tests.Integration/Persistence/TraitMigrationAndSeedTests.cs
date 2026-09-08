using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

// Imported ahead of the (still unmerged) Compêndio de Regras plan so this plan's Ficha de
// Personagem — Afeições e Características task has a real Trait table + seeded rows to reference.
// This is this branch's own coverage of that imported pipeline (migration + seed), independent of
// whatever test suite the Compêndio de Regras plan eventually brings with its own merge.
public class TraitMigrationAndSeedTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitMigrationAndSeedTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_traits_table_and_SeedAsync_populates_it_from_the_real_document()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddTraits"));

        var caracteristicasMarkdown = RulesDataProvider.ReadResource("Caracteristicas.md");
        var result = await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);

        result.Inserted.Should().BeGreaterThan(0);
        (await db.Traits.CountAsync()).Should().Be(result.Inserted);

        // Re-seeding is idempotent — running it again against already-seeded, already-correct rows
        // inserts and updates nothing new.
        var secondRun = await TraitSeeder.SeedAsync(db, caracteristicasMarkdown);
        secondRun.Inserted.Should().Be(0);
        secondRun.Updated.Should().Be(0);

        var trait = await db.Traits.FirstAsync();
        trait.Nome.Should().NotBeNullOrWhiteSpace();
        trait.Descricao.Should().NotBeNullOrWhiteSpace();
    }
}
