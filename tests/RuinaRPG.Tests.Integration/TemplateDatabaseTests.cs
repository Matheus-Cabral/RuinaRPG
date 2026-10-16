using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration;

public class TemplateDatabaseTests : IClassFixture<ApiFixture>
{
    private readonly ApiFixture _fixture;

    public TemplateDatabaseTests(ApiFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_clone_has_every_migration_and_the_seeded_rules_data()
    {
        await using var scope = _fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        (await db.Traits.CountAsync()).Should().BeGreaterThan(0);
        (await db.Historicos.CountAsync()).Should().BeGreaterThan(0);
        (await db.Efeitos.CountAsync()).Should().BeGreaterThan(0);
        (await db.EquipmentKits.CountAsync()).Should().BeGreaterThan(0);
        (await db.DurabilidadesPorRank.CountAsync()).Should().BeGreaterThan(0);
        (await db.TabelaDeAfinidades.CountAsync()).Should().BeGreaterThan(0);
        (await db.NiveisProgressao.CountAsync()).Should().BeGreaterThan(0);
    }
}
