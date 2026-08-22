using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class CharacterRuneAndMasteryMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public CharacterRuneAndMasteryMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_CharacterRunes_and_CharacterMasteries_tables()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddCharacterRunesAndMasteries"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@runemasterytest.com", Email = "gm@runemasterytest.com", Nickname = "RuneMasteryTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@runemasterytest.com", Email = "player@runemasterytest.com", Nickname = "RuneMasteryTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        await db.SaveChangesAsync();

        db.CharacterRunes.Add(new CharacterRune { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Nome = "Runa do Fogo", Descricao = "Queima o alvo.", Grau = 1 });
        db.CharacterMasteries.Add(new CharacterMastery { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Nome = "Maestria em Pontaria", Pericia = Pericia.Pontaria, Atributo = Atributo.Destreza, GastoMaestria = 3 });
        await db.SaveChangesAsync();

        (await db.CharacterRunes.CountAsync()).Should().Be(1);
        (await db.CharacterMasteries.CountAsync()).Should().Be(1);
    }
}
