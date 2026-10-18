using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.Rules;

namespace RuinaRPG.Tests.Integration.Persistence;

// Apagar uma Perícia em uso tem de falhar: todas as FKs para Pericias são DeleteBehavior.Restrict.
public class PericiasRestrictTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public PericiasRestrictTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Deleting_a_pericia_referenced_by_a_skill_a_mastery_or_a_historico_is_a_foreign_key_violation()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@restricttest.com", Email = "gm@restricttest.com", Nickname = "RestrictTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@restricttest.com", Email = "player@restricttest.com", Nickname = "RestrictTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        var criatura = new CreatureSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.CharacterSheets.Add(sheet);
        db.NpcSheets.Add(npc);
        db.CreatureSheets.Add(criatura);
        await db.SaveChangesAsync();

        // Uma Perícia distinta por referência, para cada FK ser exercitada isoladamente.
        const int characterSkill = 1, npcSkill = 2, creatureSkill = 3, characterMastery = 4, npcMastery = 5, creatureMastery = 6, maisSeis = 7, maisTres = 8;
        db.CharacterSkills.Add(new CharacterSkill { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, PericiaId = characterSkill, Gasto = 1 });
        db.NpcSkills.Add(new NpcSkill { Id = Guid.NewGuid(), NpcSheetId = npc.Id, PericiaId = npcSkill, Gasto = 1 });
        db.CreatureSkills.Add(new CreatureSkill { Id = Guid.NewGuid(), CreatureSheetId = criatura.Id, PericiaId = creatureSkill, Gasto = 1 });
        db.CharacterMasteries.Add(new CharacterMastery { Id = Guid.NewGuid(), CharacterSheetId = sheet.Id, Nome = "M1", PericiaId = characterMastery, Atributo = Atributo.Destreza, GastoMaestria = 1 });
        db.NpcMasteries.Add(new NpcMastery { Id = Guid.NewGuid(), NpcSheetId = npc.Id, Nome = "M2", PericiaId = npcMastery, Atributo = Atributo.Destreza, GastoMaestria = 1 });
        db.CreatureMasteries.Add(new CreatureMastery { Id = Guid.NewGuid(), CreatureSheetId = criatura.Id, Nome = "M3", PericiaId = creatureMastery, Atributo = AtributoCriatura.Destreza, GastoMaestria = 1 });
        db.Historicos.Add(new Historico { Id = Guid.NewGuid(), Nome = "Historico de teste", Descricao = "d", PericiaMaisSeisId = maisSeis, PericiaMaisTresId = maisTres });
        await db.SaveChangesAsync();

        foreach (var id in new[] { characterSkill, npcSkill, creatureSkill, characterMastery, npcMastery, creatureMastery, maisSeis, maisTres })
        {
            var apagar = async () => await db.Database.ExecuteSqlRawAsync("DELETE FROM \"Pericias\" WHERE \"Id\" = {0}", id);
            var erro = await apagar.Should().ThrowAsync<Exception>($"a Perícia {id} está em uso");
            var pg = erro.Which as PostgresException ?? erro.Which.InnerException as PostgresException;
            pg.Should().NotBeNull($"a Perícia {id} está em uso");
            pg!.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation, $"a Perícia {id} está em uso");
        }

        (await db.Pericias.CountAsync(p => new[] { 1, 2, 3, 4, 5, 6, 7, 8 }.Contains(p.Id))).Should().Be(8);
    }
}
