using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

// Fixture própria: o teste para o banco num ponto antigo do histórico de migrations, então não pode
// dividir o banco com testes que já migraram até o fim.
public class DurabilidadeBackfillMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public DurabilidadeBackfillMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task RemoveDurabilidadeMaximaDosItens_backfills_the_atual_of_weapons_whose_item_had_no_typed_maximum()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        var migrator = db.GetService<IMigrator>();
        var anterior = db.Database.GetMigrations().Single(m => m.EndsWith("_AddDurabilidadePorRank"));
        await migrator.MigrateAsync(anterior);

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@backfill.com", Email = "gm@backfill.com", Nickname = "BackfillGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "p@backfill.com", Email = "p@backfill.com", Nickname = "BackfillPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        db.Users.AddRange(gm, player);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        // Campanha por SQL cru: a entidade já tem colunas de migrations posteriores (ex: BonusDeCarga).
        await SchemaInsert.AtCurrentSchemaAsync(db, "Campaigns", new() { ["Id"] = campaign.Id, ["GmId"] = gm.Id, ["Nome"] = campaign.Nome, ["Descricao"] = campaign.Descricao });

        var sheetId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "CharacterSheets", new() { ["Id"] = sheetId, ["CampaignId"] = campaign.Id, ["OwnerId"] = player.Id });
        var npcId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "NpcSheets", new() { ["Id"] = npcId, ["GmId"] = gm.Id });
        var creatureId = Guid.NewGuid();
        await SchemaInsert.AtCurrentSchemaAsync(db, "CreatureSheets", new() { ["Id"] = creatureId, ["GmId"] = gm.Id });

        var armaRankD = await InsertArmaAsync(db, gm.Id, rank: 2, durabilidadeMaxima: null);
        var armaDigitada = await InsertArmaAsync(db, gm.Id, rank: 2, durabilidadeMaxima: 50);
        var armaRankS = await InsertArmaAsync(db, gm.Id, rank: 6, durabilidadeMaxima: null);
        var armaRankF = await InsertArmaAsync(db, gm.Id, rank: 0, durabilidadeMaxima: null);
        var armaSemRank = await InsertArmaAsync(db, gm.Id, rank: null, durabilidadeMaxima: null);

        var personagemD = await InsertWeaponAsync(db, "CharacterWeapons", "CharacterSheetId", sheetId, armaRankD, 0);
        var personagemDigitada = await InsertWeaponAsync(db, "CharacterWeapons", "CharacterSheetId", sheetId, armaDigitada, 30);
        var personagemS = await InsertWeaponAsync(db, "CharacterWeapons", "CharacterSheetId", sheetId, armaRankS, 0);
        var personagemSemRank = await InsertWeaponAsync(db, "CharacterWeapons", "CharacterSheetId", sheetId, armaSemRank, 0);
        var npcF = await InsertWeaponAsync(db, "NpcWeapons", "NpcSheetId", npcId, armaRankF, 0);
        var criaturaD = await InsertWeaponAsync(db, "CreatureWeapons", "CreatureSheetId", creatureId, armaRankD, 0);
        var ataqueNatural = await InsertWeaponAsync(db, "CreatureWeapons", "CreatureSheetId", creatureId, null, null);

        await migrator.MigrateAsync();

        var personagem = await db.CharacterWeapons.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.DurabilidadeAtual);
        personagem[personagemD].Should().Be(80);
        personagem[personagemDigitada].Should().Be(30);
        personagem[personagemS].Should().Be(0);
        personagem[personagemSemRank].Should().Be(0);
        (await db.NpcWeapons.AsNoTracking().SingleAsync(w => w.Id == npcF)).DurabilidadeAtual.Should().Be(20);
        var criatura = await db.CreatureWeapons.AsNoTracking().ToDictionaryAsync(w => w.Id, w => w.DurabilidadeAtual);
        criatura[criaturaD].Should().Be(80);
        criatura[ataqueNatural].Should().BeNull();
    }

    private static async Task<Guid> InsertArmaAsync(RuinaRpgDbContext db, Guid gmId, int? rank, int? durabilidadeMaxima)
    {
        var id = Guid.NewGuid();
        var values = new Dictionary<string, object> { ["Id"] = id, ["GmId"] = gmId, ["Nome"] = $"Arma {id:N}", ["Tipo"] = "Arma" };
        if (rank is not null) values["Arma_Rank"] = rank.Value;
        if (durabilidadeMaxima is not null) values["Arma_DurabilidadeMaxima"] = durabilidadeMaxima.Value;
        await SchemaInsert.AtCurrentSchemaAsync(db, "Items", values);
        return id;
    }

    private static async Task<Guid> InsertWeaponAsync(RuinaRpgDbContext db, string table, string sheetColumn, Guid sheetId, Guid? itemId, int? atual)
    {
        var id = Guid.NewGuid();
        var values = new Dictionary<string, object> { ["Id"] = id, [sheetColumn] = sheetId };
        if (itemId is not null) values["ItemId"] = itemId.Value;
        if (atual is not null) values["DurabilidadeAtual"] = atual.Value;
        await SchemaInsert.AtCurrentSchemaAsync(db, table, values);
        return id;
    }
}
