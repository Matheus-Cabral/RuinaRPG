using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Invites;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class InviteCodeMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public InviteCodeMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_InviteCodes_table_with_a_unique_index_on_Code()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;

        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddInviteCodes"));

        var gm = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "gm@migrationtest.com",
            Email = "gm@migrationtest.com",
            Nickname = "MigrationGm",
            Role = UserRole.GM
        };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.InviteCodes.Add(new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "DUPLICAT",
            GmId = gm.Id,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        });
        await db.SaveChangesAsync();

        db.InviteCodes.Add(new InviteCode
        {
            Id = Guid.NewGuid(),
            Code = "DUPLICAT",
            GmId = gm.Id,
            GeneratedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(48)
        });

        var act = async () => await db.SaveChangesAsync();
        await act.Should().ThrowAsync<DbUpdateException>();
    }
}
