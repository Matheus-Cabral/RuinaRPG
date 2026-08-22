using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterSheetMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterSheetMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CharacterSheets_table_with_a_default_Nivel_of_1()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterSheets"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@sheettest.com", Email = "gm@sheettest.com", Nickname = "SheetTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@sheettest.com", Email = "player@sheettest.com", Nickname = "SheetTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id, Linhagem = Linhagem.Humano, Variante = Variante.Sinir };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        var reloaded = await db.CharacterSheets.SingleAsync();
        reloaded.Nivel.Should().Be(1);
        reloaded.Linhagem.Should().Be(Linhagem.Humano);
    }
}
