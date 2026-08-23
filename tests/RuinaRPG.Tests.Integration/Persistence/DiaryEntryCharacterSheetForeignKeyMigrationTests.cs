using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Diary;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class DiaryEntryCharacterSheetForeignKeyMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DiaryEntryCharacterSheetForeignKeyMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Deleting_a_character_sheet_cascades_to_its_diary_entries()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddDiaryEntryCharacterSheetForeignKey"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@diaryfktest.com", Email = "gm@diaryfktest.com", Nickname = "DiaryFkTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@diaryfktest.com", Email = "player@diaryfktest.com", Nickname = "DiaryFkTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        var entry = new DiaryEntry { Id = Guid.NewGuid(), AuthorUserId = player.Id, CharacterSheetId = sheet.Id, IsSecretNote = false, Texto = "Entrada da ficha.", CreatedAt = DateTime.UtcNow };
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync();

        (await db.DiaryEntries.CountAsync(d => d.CharacterSheetId == sheet.Id)).Should().Be(1);

        db.CharacterSheets.Remove(sheet);
        await db.SaveChangesAsync();

        (await db.DiaryEntries.CountAsync(d => d.Id == entry.Id)).Should().Be(0);
    }
}
