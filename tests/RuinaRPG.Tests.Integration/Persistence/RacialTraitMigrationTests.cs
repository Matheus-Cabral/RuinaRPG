using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

public class RacialTraitMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public RacialTraitMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_RacialTraitOverride_table_and_the_IsRacial_RacialVariante_columns()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync();
        appliedMigrations.Should().Contain(m => m.EndsWith("AddRacialTraits"));

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@racialtraittest.com", Email = "gm@racialtraittest.com", Nickname = "RacialTraitTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@racialtraittest.com", Email = "player@racialtraittest.com", Nickname = "RacialTraitTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();

        db.RacialTraitOverrides.Add(new RacialTraitOverride
        {
            Id = Guid.NewGuid(), GmId = gm.Id, Variante = Variante.Alora,
            GratuitaOptionsJson = "[{\"TraitNome\":\"Detectar Magia\"}]",
            ObrigatoriaOptionsJson = "[{\"TraitNome\":\"Desvantagem Elemental\",\"Especificacao\":\"Fogo\"}]",
        });
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        db.CharacterSheets.Add(sheet);
        var npcSheet = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.NpcSheets.Add(npcSheet);
        var trait = new Trait { Id = Guid.NewGuid(), Nome = "Alfabetizado", Descricao = "Teste", Custo = 1, Polaridade = Polaridade.Positiva };
        db.Traits.Add(trait);
        await db.SaveChangesAsync();

        db.CharacterTraits.Add(new CharacterTrait { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, TraitId = trait.Id, Polaridade = Polaridade.Positiva, IsRacial = true, RacialVariante = Variante.Sinir });
        db.NpcTraits.Add(new NpcTrait { Id = Guid.NewGuid(), NpcSheetId = npcSheet.Id, TraitId = trait.Id, Polaridade = Polaridade.Positiva, IsRacial = true, RacialVariante = Variante.Sinir });
        await db.SaveChangesAsync();

        var overrideRow = await db.RacialTraitOverrides.SingleAsync(o => o.GmId == gm.Id);
        overrideRow.Variante.Should().Be(Variante.Alora);
        overrideRow.GratuitaOptionsJson.Should().Contain("Detectar Magia");

        var characterTraitRow = await db.CharacterTraits.SingleAsync(t => t.CharacterSheetId == sheet.Id);
        characterTraitRow.IsRacial.Should().BeTrue();
        characterTraitRow.RacialVariante.Should().Be(Variante.Sinir);

        var npcTraitRow = await db.NpcTraits.SingleAsync(t => t.NpcSheetId == npcSheet.Id);
        npcTraitRow.IsRacial.Should().BeTrue();
        npcTraitRow.RacialVariante.Should().Be(Variante.Sinir);
    }
}
