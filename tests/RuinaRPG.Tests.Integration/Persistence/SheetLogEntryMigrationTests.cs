using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SheetLogs;

namespace RuinaRPG.Tests.Integration.Persistence;

public class SheetLogEntryMigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public SheetLogEntryMigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    private static SheetLogEntry Entrada(Guid atorId, Guid? personagem = null, Guid? npc = null, Guid? criatura = null) => new()
    {
        Id = Guid.NewGuid(),
        CharacterSheetId = personagem,
        NpcSheetId = npc,
        CreatureSheetId = criatura,
        OccurredAt = DateTime.UtcNow,
        ActorUserId = atorId,
        ActorNickname = "Ator",
        ActorIsGm = true,
        Categoria = SheetLogCategoria.Geral,
        Acao = SheetLogAcao.Alterou,
        Secao = "Atributos",
        Campo = "Força",
        ValorAntes = "1",
        ValorDepois = "2"
    };

    [Fact]
    public async Task Migrate_creates_SheetLogEntries_that_cascade_with_each_kind_of_sheet_and_is_indexed_by_sheet_and_time()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@sheetlogtest.com", Email = "gm@sheetlogtest.com", Nickname = "SheetLogTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@sheetlogtest.com", Email = "player@sheetlogtest.com", Nickname = "SheetLogTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        var personagem = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        var npc = new NpcSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        var criatura = new CreatureSheet { Id = Guid.NewGuid(), GmId = gm.Id };
        db.CharacterSheets.Add(personagem);
        db.NpcSheets.Add(npc);
        db.CreatureSheets.Add(criatura);
        await db.SaveChangesAsync();

        db.SheetLogEntries.AddRange(
            Entrada(gm.Id, personagem: personagem.Id),
            Entrada(gm.Id, npc: npc.Id),
            Entrada(gm.Id, criatura: criatura.Id));
        await db.SaveChangesAsync();
        (await db.SheetLogEntries.CountAsync(l => l.ActorUserId == gm.Id)).Should().Be(3);

        db.CharacterSheets.Remove(personagem);
        db.NpcSheets.Remove(npc);
        db.CreatureSheets.Remove(criatura);
        await db.SaveChangesAsync();

        (await db.SheetLogEntries.CountAsync(l => l.ActorUserId == gm.Id)).Should().Be(0);

        var indices = await db.Database.SqlQuery<string>(
            $"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'SheetLogEntries'").ToListAsync();
        indices.Should().Contain(d => d.Contains("\"CharacterSheetId\"") && d.Contains("\"OccurredAt\""));
        indices.Should().Contain(d => d.Contains("\"NpcSheetId\"") && d.Contains("\"OccurredAt\""));
        indices.Should().Contain(d => d.Contains("\"CreatureSheetId\"") && d.Contains("\"OccurredAt\""));
    }
}
