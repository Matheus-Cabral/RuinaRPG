using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CreatureSheetMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CreatureSheetMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CreatureSheets_table_with_a_default_Nivel_of_1_and_a_null_OwnerId()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCreatureSheets"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@creaturesheettest.com", Email = "gm@creaturesheettest.com", Nickname = "CreatureSheetTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        var sheet = new CreatureSheet { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Lobo das Ruínas" };
        db.CreatureSheets.Add(sheet);
        await db.SaveChangesAsync();

        var reloaded = await db.CreatureSheets.SingleAsync();
        reloaded.Nivel.Should().Be(1);
        reloaded.Nome.Should().Be("Lobo das Ruínas");
        reloaded.OwnerId.Should().BeNull();
    }
}
