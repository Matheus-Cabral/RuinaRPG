using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RulesAuditorMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RulesAuditorMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_IsRulesAuditor_column_defaulted_to_false()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddIsRulesAuditor"));

        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "auditortest@teste.com", Email = "auditortest@teste.com", Nickname = "AuditorMigrationTest", Role = RuinaRPG.Domain.Enums.UserRole.GM };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        (await db.Users.SingleAsync(u => u.Id == user.Id)).IsRulesAuditor.Should().BeFalse();

        user.IsRulesAuditor = true;
        await db.SaveChangesAsync();
        (await db.Users.SingleAsync(u => u.Id == user.Id)).IsRulesAuditor.Should().BeTrue();
    }
}
