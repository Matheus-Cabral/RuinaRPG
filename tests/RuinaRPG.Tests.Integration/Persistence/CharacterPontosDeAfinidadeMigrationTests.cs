using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterPontosDeAfinidadeMigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public CharacterPontosDeAfinidadeMigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CharacterPontosDeAfinidade_table()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@afftest.com", Email = "gm@afftest.com", Nickname = "AffTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@afftest.com", Email = "player@afftest.com", Nickname = "AffTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.CharacterPontosDeAfinidade.Add(new CharacterPontosDeAfinidade { Id = Guid.NewGuid(), SheetId = sheet.Id, ElementoId = 2 /* Fogo */, Pontos = 3, Experiencia = 10 });
        await db.SaveChangesAsync();

        (await db.CharacterPontosDeAfinidade.CountAsync()).Should().Be(1);
    }
}
