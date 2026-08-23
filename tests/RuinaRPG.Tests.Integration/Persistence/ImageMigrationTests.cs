using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Images;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class ImageMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public ImageMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Images_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddImages"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@imgtest.com", Email = "gm@imgtest.com", Nickname = "ImgTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Images.Add(new Image { Id = Guid.NewGuid(), Path = "abc123.png", ContentType = "image/png", UploadedByUserId = gm.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        (await db.Images.CountAsync()).Should().Be(1);
    }
}
