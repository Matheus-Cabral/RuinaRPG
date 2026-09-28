using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Infrastructure.SpellsAndAbilities;

namespace RuinaRPG.Tests.Integration.Persistence;

public class HabilidadesPassivasMigrationTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public HabilidadesPassivasMigrationTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Categoria_and_requisitos_round_trip_on_the_bank_and_on_a_sheet_copy()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using (var db = new RuinaRpgDbContext(options))
        {
            await db.Database.MigrateAsync();
            (await db.Database.GetAppliedMigrationsAsync()).Should().Contain(m => m.EndsWith("AddHabilidadesPassivas"));
        }

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@passivatest.com", Email = "gm@passivatest.com", Nickname = "PassivaTestGm", Role = UserRole.GM };
        var player = new ApplicationUser { Id = Guid.NewGuid(), UserName = "player@passivatest.com", Email = "player@passivatest.com", Nickname = "PassivaTestPlayer", Role = UserRole.Jogador, InvitedByGmId = gm.Id };
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Teste", Descricao = "" };
        var sheet = new CharacterSheet { Id = Guid.NewGuid(), CampaignId = campaign.Id, OwnerId = player.Id };
        var requisitos = new RequisitosDePassiva
        {
            Nivel = 3, Vocacao = Vocacao.Feiticeiro, Classe = "Elementalista",
            Atributos = [new(Atributo.Forca, 4)], SubAtributos = [new(SubAtributo.Iniciativa, 2)], Pericias = [new(Pericia.Atletismo, 5)]
        };
        var bankId = Guid.NewGuid();
        var sheetEntryId = Guid.NewGuid();

        await using (var db = new RuinaRpgDbContext(options))
        {
            db.Users.AddRange(gm, player);
            await db.SaveChangesAsync();
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync();
            db.CharacterSheets.Add(sheet);
            db.SpellAbilityBankEntries.Add(new SpellAbilityBankEntry
            {
                Id = bankId, GmId = gm.Id, Nome = "Pele de Pedra", Tipo = SpellAbilityTipo.Passiva, Descricao = "d",
                Categoria = CategoriaDePassiva.Vocacional, Requisitos = requisitos
            });
            db.CharacterSpellAbilities.Add(new CharacterSpellAbility
            {
                Id = sheetEntryId, CharacterSheetId = sheet.Id, SourceBankEntryId = bankId, Nome = "Pele de Pedra",
                Tipo = SpellAbilityTipo.Passiva, Descricao = "d", Categoria = CategoriaDePassiva.Vocacional, Requisitos = requisitos
            });
            await db.SaveChangesAsync();
        }

        await using (var db = new RuinaRpgDbContext(options))
        {
            var bank = await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId);
            bank.Categoria.Should().Be(CategoriaDePassiva.Vocacional);
            bank.Requisitos.Should().BeEquivalentTo(requisitos);

            var copy = await db.CharacterSpellAbilities.SingleAsync(e => e.Id == sheetEntryId);
            copy.Requisitos.Should().BeEquivalentTo(requisitos);

            // Uma Magia comum continua sem Categoria/Requisitos.
            var magia = new SpellAbilityBankEntry { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "Bola", Tipo = SpellAbilityTipo.Magia, Descricao = "d" };
            db.SpellAbilityBankEntries.Add(magia);
            await db.SaveChangesAsync();
            (await db.SpellAbilityBankEntries.AsNoTracking().SingleAsync(e => e.Id == magia.Id)).Requisitos.Should().BeNull();
        }

        await using (var db = new RuinaRpgDbContext(options))
        {
            // Mudar um item de uma lista dentro do jsonb precisa ser detectado (comparer por valor).
            var bank = await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId);
            bank.Requisitos = bank.Requisitos! with { Nivel = 7 };
            await db.SaveChangesAsync();
        }

        await using (var db = new RuinaRpgDbContext(options))
            (await db.SpellAbilityBankEntries.SingleAsync(e => e.Id == bankId)).Requisitos!.Nivel.Should().Be(7);
    }
}
