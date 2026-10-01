using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

// Fixture própria: o teste para o banco num ponto antigo do histórico de migrations, então não pode
// dividir o banco com testes que já migraram até o fim.
public class PericiasComoTabelaMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public PericiasComoTabelaMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task PericiasComoTabela_keeps_the_values_written_under_the_enum_columns_and_restricts_deleting_a_pericia_in_use()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddPericias"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@pericias.com", Email = "gm@pericias.com", Nickname = "PericiasGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@pericias.com", Email = "p@pericias.com", Nickname = "PericiasPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var sheetId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "CharacterSheets", new() { ["Id"] = sheetId, ["CampaignId"] = campaign.Id, ["OwnerId"] = player.Id });
        var npcId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "NpcSheets", new() { ["Id"] = npcId, ["GmId"] = gm.Id });
        var creatureId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "CreatureSheets", new() { ["Id"] = creatureId, ["GmId"] = gm.Id });

        // Colunas antigas, com o valor int do enum: Atletismo = 7, Pontaria = 31, Arcano = 2, Biblioteca = 9.
        await SchemaInsert.AtCurrentSchemaAsync(db, "CharacterSkills", new() { ["Id"] = Guid.NewGuid(), ["CharacterSheetId"] = sheetId, ["Pericia"] = 7, ["Gasto"] = 6 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "NpcSkills", new() { ["Id"] = Guid.NewGuid(), ["NpcSheetId"] = npcId, ["Pericia"] = 7, ["Gasto"] = 5 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "CreatureSkills", new() { ["Id"] = Guid.NewGuid(), ["CreatureSheetId"] = creatureId, ["Pericia"] = 7, ["Gasto"] = 4 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "CharacterMasteries", new() { ["Id"] = Guid.NewGuid(), ["CharacterSheetId"] = sheetId, ["Nome"] = "Maestria P", ["Pericia"] = 31 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "NpcMasteries", new() { ["Id"] = Guid.NewGuid(), ["NpcSheetId"] = npcId, ["Nome"] = "Maestria N", ["Pericia"] = 31 });
        await SchemaInsert.AtCurrentSchemaAsync(db, "CreatureMasteries", new() { ["Id"] = Guid.NewGuid(), ["CreatureSheetId"] = creatureId, ["Nome"] = "Maestria C", ["Pericia"] = 31 });
        var historicoId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "Historicos", new() { ["Id"] = historicoId, ["Nome"] = "Estudo de Teste", ["Descricao"] = "d", ["PericiaMaisSeis"] = 2, ["PericiaMaisTres"] = 9 });

        await migrator.MigrateAsync();

        var skill = await db.CharacterSkills.AsNoTracking().SingleAsync(s => s.CharacterSheetId == sheetId);
        (skill.PericiaId, skill.Gasto).Should().Be((7, 6));
        var npcSkill = await db.NpcSkills.AsNoTracking().SingleAsync(s => s.NpcSheetId == npcId);
        (npcSkill.PericiaId, npcSkill.Gasto).Should().Be((7, 5));
        var creatureSkill = await db.CreatureSkills.AsNoTracking().SingleAsync(s => s.CreatureSheetId == creatureId);
        (creatureSkill.PericiaId, creatureSkill.Gasto).Should().Be((7, 4));
        (await db.CharacterMasteries.AsNoTracking().SingleAsync(m => m.CharacterSheetId == sheetId)).PericiaId.Should().Be(31);
        (await db.NpcMasteries.AsNoTracking().SingleAsync(m => m.NpcSheetId == npcId)).PericiaId.Should().Be(31);
        (await db.CreatureMasteries.AsNoTracking().SingleAsync(m => m.CreatureSheetId == creatureId)).PericiaId.Should().Be(31);
        var historico = await db.Historicos.AsNoTracking().SingleAsync(h => h.Id == historicoId);
        (historico.PericiaMaisSeisId, historico.PericiaMaisTresId).Should().Be((2, 9));

        // FK ON DELETE RESTRICT: uma Perícia referenciada por ficha ou Histórico não pode ser apagada.
        foreach (var id in new[] { 7, 31, 2, 9 })
        {
            var apagar = () => db.Database.ExecuteSqlAsync($"DELETE FROM \"Pericias\" WHERE \"Id\" = {id}");
            (await apagar.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(PostgresErrorCodes.ForeignKeyViolation);
        }
    }
}
