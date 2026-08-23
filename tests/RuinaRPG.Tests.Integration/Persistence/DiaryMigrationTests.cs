using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DiaryMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DiaryMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_diary_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddDiary"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@diarytest.com", Email = "gm@diarytest.com", Nickname = "DiaryTestGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var entry = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = gm.Id, CampaignId = campaign.Id, IsSecretNote = false, Texto = "Primeira entrada.", CreatedAt = DateTime.UtcNow };
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.DiaryEntries.CountAsync()).Should().Be(1);
    }
}
