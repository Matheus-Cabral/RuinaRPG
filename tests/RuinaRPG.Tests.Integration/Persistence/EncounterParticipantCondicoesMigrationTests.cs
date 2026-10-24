using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Domain.Condicoes;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Encounters;
using RuinaRPG.Infrastructure.Identity;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Persistence;

public class EncounterParticipantCondicoesMigrationTests : IClassFixture<BlankDatabaseFixture>
{
    private readonly BlankDatabaseFixture _fixture;

    public EncounterParticipantCondicoesMigrationTests(BlankDatabaseFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Migrate_creates_the_table_with_jsonb_columns_a_cascading_participant_FK_and_round_trips_the_snapshot()
    {
        var options = new DbContextOptionsBuilder<RuinaRpgDbContext>().UseNpgsql(_fixture.ConnectionString).Options;
        await using var db = new RuinaRpgDbContext(options);
        await db.Database.MigrateAsync();

        var tipos = await db.Database.SqlQueryRaw<string>(
            "SELECT column_name || ':' || data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'EncounterParticipantCondicoes' AND data_type = 'jsonb'").ToListAsync();
        tipos.Should().BeEquivalentTo("Modificadores:jsonb", "Desabilitacoes:jsonb", "AjustesAplicados:jsonb");
        var cascata = await db.Database.SqlQueryRaw<string>(
            "SELECT constraint_name AS \"Value\" FROM information_schema.referential_constraints WHERE delete_rule = 'CASCADE' AND constraint_name LIKE 'FK_EncounterParticipantCondicoes%'").ToListAsync();
        cascata.Should().ContainSingle();
        var indice = await db.Database.SqlQueryRaw<string>(
            "SELECT indexname AS \"Value\" FROM pg_indexes WHERE indexname = 'IX_EncounterParticipantCondicoes_EncounterParticipantId'").ToListAsync();
        indice.Should().ContainSingle();

        var gm = new ApplicationUser { Id = Guid.NewGuid(), UserName = "gm@condicoespart.com", Email = "gm@condicoespart.com", Nickname = "CondPartGm", Role = UserRole.GM };
        db.Users.Add(gm);
        await db.SaveChangesAsync();
        var campaign = new Campaign { Id = Guid.NewGuid(), GmId = gm.Id, Nome = "C", Descricao = "" };
        var encounter = new Encounter { Id = Guid.NewGuid(), CampaignId = campaign.Id };
        var participant = new EncounterParticipant { Id = Guid.NewGuid(), EncounterId = encounter.Id, Nome = "Goblin" };
        db.Campaigns.Add(campaign);
        db.Encounters.Add(encounter);
        db.EncounterParticipants.Add(participant);
        var linha = Guid.NewGuid();
        var item = Guid.NewGuid();
        db.EncounterParticipantCondicoes.Add(new EncounterParticipantCondicao
        {
            Id = Guid.NewGuid(), EncounterParticipantId = participant.Id, Nome = "Veneno", Icone = "Veneno", AplicadaNoTurno = 2, DuracaoTurnos = 3,
            Modificadores = [new LinhaDeCondicaoAplicada(linha, AlvoDeCondicao.Atributo, "Vigor", TipoDeValorDeCondicao.Fixo, -2, null)],
            Desabilitacoes = new() { [CategoriaDesabilitavel.Arma] = [item], [CategoriaDesabilitavel.Runa] = [] },
            AjustesAplicados = [new AjusteAplicado(linha, 2)],
        });
        await db.SaveChangesAsync();

        await using var leitura = new RuinaRpgDbContext(options);
        var lida = await leitura.EncounterParticipantCondicoes.SingleAsync(c => c.EncounterParticipantId == participant.Id);
        lida.Modificadores.Should().Equal(new LinhaDeCondicaoAplicada(linha, AlvoDeCondicao.Atributo, "Vigor", TipoDeValorDeCondicao.Fixo, -2, null));
        lida.Desabilitacoes[CategoriaDesabilitavel.Arma].Should().Equal(item);
        lida.Desabilitacoes[CategoriaDesabilitavel.Runa].Should().BeEmpty();
        lida.AjustesAplicados.Should().Equal(new AjusteAplicado(linha, 2));

        // A change inside a list is detected (value comparer), and the encounter's cascade takes the row with it.
        lida.Modificadores[0] = lida.Modificadores[0] with { Valor = -5 };
        lida.AjustesAplicados.Add(new AjusteAplicado(linha, 3));
        await leitura.SaveChangesAsync();
        await using var releitura = new RuinaRpgDbContext(options);
        var relida = await releitura.EncounterParticipantCondicoes.SingleAsync(c => c.EncounterParticipantId == participant.Id);
        relida.Modificadores.Single().Valor.Should().Be(-5);
        relida.AjustesAplicados.Should().HaveCount(2);

        releitura.Encounters.Remove(await releitura.Encounters.SingleAsync(e => e.Id == encounter.Id));
        await releitura.SaveChangesAsync();
        (await releitura.EncounterParticipantCondicoes.AnyAsync(c => c.EncounterParticipantId == participant.Id)).Should().BeFalse();
    }
}
