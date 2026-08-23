using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CampaignMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CampaignMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_Campaigns_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCampaigns"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@camptest.com", Email = "gm@camptest.com", Nickname = "CampTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();

        db.Campaigns.Add(new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "A Ruína Aguarda", Descricao = "Uma campanha de teste." });
        await db.SaveChangesAsync();

        (await db.Campaigns.CountAsync()).Should().Be(1);
    }
}
