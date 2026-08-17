using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class TraitMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public TraitMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Traits_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddTraits"));

        db.Traits.Add(new Trait { Id = Guid.NewGuid(), Nome = "Teste", Descricao = "Descrição de teste", Custo = 2, Polaridade = Polaridade.Positiva });
        await db.SaveChangesAsync();

        (await db.Traits.CountAsync()).Should().Be(1);
    }
}
