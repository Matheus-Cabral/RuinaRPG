using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class MigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public MigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_leaves_nothing_pending_and_the_model_matches_the_migrations()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        (await db.Database.GetPendingMigrationsAsync()).Should().BeEmpty();
        db.Database.HasPendingModelChanges().Should().BeFalse();
        (await db.Users.CountAsync()).Should().Be(0);
        (await db.RefreshTokens.CountAsync()).Should().Be(0);
    }
}
