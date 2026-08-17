using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class ItemMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ItemMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_one_Items_table_holding_all_five_types()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddItems"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@itemtest.com", Email = "gm@itemtest.com", Nickname = "ItemTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Set<Arma>().Add(new Arma { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Espada Curta", Peso = 1.5m, Preco = 50, Tier = Tier.F, Dano = 3 });
        db.Set<ItemGeral>().Add(new ItemGeral { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Corda", Peso = 0.5m, Preco = 5 });
        await db.SaveChangesAsync();

        (await db.Items.CountAsync()).Should().Be(2);
        (await db.Set<Arma>().CountAsync()).Should().Be(1);
        (await db.Set<ItemGeral>().CountAsync()).Should().Be(1);
    }
}
