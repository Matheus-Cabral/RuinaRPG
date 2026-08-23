using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class NpcSheetMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public NpcSheetMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_NpcSheets_table_with_a_default_Nivel_of_1_and_a_null_OwnerId()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddNpcSheets"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@npcsheettest.com", Email = "gm@npcsheettest.com", Nickname = "NpcSheetTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var sheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id, Linhagem = Linhagem.Humano, Variante = Variante.Sinir };
        db.NpcSheets.Add(sheet);
        await db.SaveChangesAsync();

        var reloaded = await db.NpcSheets.SingleAsync();
        reloaded.Nivel.Should().Be(1);
        reloaded.Linhagem.Should().Be(Linhagem.Humano);
        reloaded.OwnerId.Should().BeNull();
    }
}
