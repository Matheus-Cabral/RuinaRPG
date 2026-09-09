using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RacialAbilityMigrationAndArcaEntryTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RacialAbilityMigrationAndArcaEntryTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_RacialAbilityOverride_and_ArcaEntry_tables_and_the_ArcaRolada_columns()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRacialAbilityOverrideAndArcaEntry"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@racialtest.com", Email = "gm@racialtest.com", Nickname = "RacialTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@racialtest.com", Email = "player@racialtest.com", Nickname = "RacialTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        db.RacialAbilityOverrides.Add(new RacialAbilityOverride { Id = Guid.NewGuid(), GmId = gm.Id, Variante = Variante.Alora, Nome = "Racial (Custom)", Descricao = "Texto custom." });
        db.ArcaEntries.Add(new ArcaEntry { Id = Guid.NewGuid(), GmId = gm.Id, Roll = 7, Nome = "A Chama Eterna", Descricao = "Efeito de teste." });
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id, ArcaRolada = 7 };
        db.CharacterSheets.Add(sheet);
        var npcSheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id, ArcaRolada = 3 };
        db.NpcSheets.Add(npcSheet);
        await db.SaveChangesAsync();

        var overrideRow = await db.RacialAbilityOverrides.SingleAsync(o => o.GmId == gm.Id);
        overrideRow.Variante.Should().Be(Variante.Alora);
        overrideRow.Nome.Should().Be("Racial (Custom)");

        var arcaRow = await db.ArcaEntries.SingleAsync(a => a.GmId == gm.Id);
        arcaRow.Roll.Should().Be(7);
        arcaRow.Nome.Should().Be("A Chama Eterna");

        (await db.CharacterSheets.SingleAsync(s => s.Id == sheet.Id)).ArcaRolada.Should().Be(7);
        (await db.NpcSheets.SingleAsync(s => s.Id == npcSheet.Id)).ArcaRolada.Should().Be(3);
    }
}
