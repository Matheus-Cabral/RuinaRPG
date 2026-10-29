using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Diary;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Infrastructure.Campaigns;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Campanha R0018 e Gerenciador de Encontros R0008: o GM remove um jogador da campanha. "Aria" é a Ficha de Personagem
/// do jogador; "Fera" (NPC) e "Garra" (Criatura) estão concedidas a ele; "Lobo" é um NPC do GM; "Bran" é a Ficha de
/// Personagem de outro jogador, que fica.
/// </summary>
public class RemoverMembroTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private sealed record Mesa(string Gm, string JogadorId, string Jogador, string OutroId, string CampaignId, string Aria, string Fera, string Garra, string Lobo, string Bran);

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null, HttpStatusCode? esperado = null)
    {
        var response = await Client.SendAsync(AuthedRequest(method, url, token, body));
        if (esperado is { } status)
            response.StatusCode.Should().Be(status, $"{method} {url}: {await response.Content.ReadAsStringAsync()}");
        else
            response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} devolveu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<T> LerAsync<T>(string token, string url) =>
        (await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadFromJsonAsync<T>())!;

    private Task AdicionarMembroAsync(string gm, string campaignId, string userId) =>
        EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(userId));

    private Task<HttpResponseMessage> RemoverAsync(string token, string campaignId, string userId) =>
        Client.SendAsync(AuthedRequest(HttpMethod.Delete, $"/api/campaigns/{campaignId}/members/{userId}", token));

    private async Task<string> PersonagemAsync(string gm, string campaignId, string donoId, string nome)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/character-sheets", gm, new CreateCharacterSheetRequest(donoId)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.CharacterSheets.FindAsync(Guid.Parse(id)))!.Nome = nome;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<string> ConcederAsync(string gm, string campaignId, string jogadorId, string tipo, string nome)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/grants", gm, new GrantSheetRequest(jogadorId, tipo, null)))
            .Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        if (tipo == "Npc")
            (await db.NpcSheets.FindAsync(Guid.Parse(id)))!.Nome = nome;
        else
            (await db.CreatureSheets.FindAsync(Guid.Parse(id)))!.Nome = nome;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<string> NpcDoGmAsync(string gm, string nome)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>())!.Id;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.NpcSheets.FindAsync(Guid.Parse(id)))!.Nome = nome;
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<Mesa> MesaAsync()
    {
        var gm = await RegisterGmAndGetTokenAsync(Unique("RmGm"), Unique("rmgm") + "@t.com");
        var (jogadorId, jogador) = await RegisterJogadorLinkedToAsync(gm, Unique("RmJg"), Unique("rmjg") + "@t.com");
        var (outroId, _) = await RegisterJogadorLinkedToAsync(gm, Unique("RmOu"), Unique("rmou") + "@t.com");
        var campaignId = await CreateCampaignAsync(gm, "Campanha");
        await AdicionarMembroAsync(gm, campaignId, jogadorId);
        await AdicionarMembroAsync(gm, campaignId, outroId);
        return new Mesa(gm, jogadorId, jogador, outroId, campaignId,
            await PersonagemAsync(gm, campaignId, jogadorId, "Aria"),
            await ConcederAsync(gm, campaignId, jogadorId, "Npc", "Fera"),
            await ConcederAsync(gm, campaignId, jogadorId, "Creature", "Garra"),
            await NpcDoGmAsync(gm, "Lobo"),
            await PersonagemAsync(gm, campaignId, outroId, "Bran"));
    }

    private async Task<string> EncontroAsync(Mesa m, params (string Nome, int Iniciativa)[] participantes)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{m.CampaignId}/encounters", m.Gm, new CreateEncounterRequest("Encontro")))
            .Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
        foreach (var (nome, iniciativa) in participantes)
        {
            var request = nome switch
            {
                "Aria" => new AddParticipantRequest(m.Aria, null, null, iniciativa),
                "Bran" => new AddParticipantRequest(m.Bran, null, null, iniciativa),
                "Fera" => new AddParticipantRequest(null, m.Fera, null, iniciativa),
                "Lobo" => new AddParticipantRequest(null, m.Lobo, null, iniciativa),
                "Garra" => new AddParticipantRequest(null, null, m.Garra, iniciativa),
                _ => throw new ArgumentException(nome),
            };
            await EnviarAsync(HttpMethod.Post, $"/api/encounters/{id}/participants", m.Gm, request, HttpStatusCode.Created);
        }
        return id;
    }

    private Task AcaoAsync(Mesa m, string encounterId, string acao) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/{acao}", m.Gm, esperado: HttpStatusCode.NoContent);

    private Task<List<EncounterParticipantResponse>> ParticipantesAsync(Mesa m, string encounterId) =>
        LerAsync<List<EncounterParticipantResponse>>(m.Gm, $"/api/encounters/{encounterId}/participants");

    private async Task<EncounterResponse> EncontroLidoAsync(Mesa m, string encounterId) =>
        (await LerAsync<List<EncounterResponse>>(m.Gm, $"/api/campaigns/{m.CampaignId}/encounters")).Single(e => e.Id == encounterId);

    private Task<List<EncounterLogEntryResponse>> LogAsync(Mesa m, string encounterId) =>
        LerAsync<List<EncounterLogEntryResponse>>(m.Gm, $"/api/encounters/{encounterId}/log");

    private async Task<(Guid? Fera, Guid? Garra)> DonosAsync(Mesa m)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        return ((await db.NpcSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(m.Fera))).OwnerId,
            (await db.CreatureSheets.AsNoTracking().SingleAsync(s => s.Id == Guid.Parse(m.Garra))).OwnerId);
    }

    [Fact]
    public async Task Remover_membro_poe_as_fichas_no_cemiterio()
    {
        var m = await MesaAsync();

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var aria = await LerAsync<CharacterSheetResponse>(m.Jogador, $"/api/character-sheets/{m.Aria}");
        aria.NoCemiterio.Should().BeTrue();
        aria.MotivosDoCemiterio.Should().Equal("RemovidoDaCampanha");
        (await LerAsync<CharacterSheetResponse>(m.Gm, $"/api/character-sheets/{m.Bran}")).NoCemiterio.Should().BeFalse();
        (await LerAsync<List<CampaignMemberResponse>>(m.Gm, $"/api/campaigns/{m.CampaignId}/members")).Select(x => x.UserId).Should().Equal(m.OutroId);
    }

    [Fact]
    public async Task Concessoes_voltam_ao_GM()
    {
        var m = await MesaAsync();
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{m.Fera}", m.Jogador))).StatusCode.Should().Be(HttpStatusCode.OK);

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await DonosAsync(m)).Should().Be((null, null));
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var anexos = await db.CampaignAttachments.AsNoTracking()
                .Where(a => a.CampaignId == Guid.Parse(m.CampaignId) && (a.NpcSheetId == Guid.Parse(m.Fera) || a.CreatureSheetId == Guid.Parse(m.Garra)))
                .ToListAsync();
            anexos.Should().HaveCount(2);
            anexos.Should().OnlyContain(a => !a.IsPublic && !a.NpcNomePublico && !a.NpcImagemPublica && !a.CreatureNomePublico && !a.CreatureImagemPublica);
        }
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{m.Fera}", m.Jogador))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/creature-sheets/{m.Garra}", m.Jogador))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Concessao_anexada_a_outra_campanha_dele_nao_muda()
    {
        var m = await MesaAsync();
        var outraDele = await CreateCampaignAsync(m.Gm, "Outra dele");
        await AdicionarMembroAsync(m.Gm, outraDele, m.JogadorId);
        var semEle = await CreateCampaignAsync(m.Gm, "Sem ele");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = Guid.Parse(outraDele), NpcSheetId = Guid.Parse(m.Fera) });
            db.CampaignAttachments.Add(new CampaignAttachment { Id = Guid.NewGuid(), CampaignId = Guid.Parse(semEle), CreatureSheetId = Guid.Parse(m.Garra) });
            await db.SaveChangesAsync();
        }

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Fera segue anexada a uma campanha em que ele é membro: fica com ele. Garra só a uma em que ele não é: volta ao GM.
        (await DonosAsync(m)).Should().Be((Guid.Parse(m.JogadorId), null));
    }

    [Fact]
    public async Task Sai_dos_encontros_ativos_e_fica_nos_finalizados()
    {
        var m = await MesaAsync();
        (string, int)[] todos = [("Aria", 50), ("Fera", 40), ("Garra", 30), ("Lobo", 20), ("Bran", 10)];
        var preparacao = await EncontroAsync(m, todos);
        var andamento = await EncontroAsync(m, todos);
        await AcaoAsync(m, andamento, "start");
        var finalizado = await EncontroAsync(m, todos);
        await AcaoAsync(m, finalizado, "start");
        await AcaoAsync(m, finalizado, "finish");

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await ParticipantesAsync(m, preparacao)).Select(p => p.Nome).Should().Equal("Lobo", "Bran");
        (await ParticipantesAsync(m, andamento)).Select(p => p.Nome).Should().Equal("Lobo", "Bran");
        (await ParticipantesAsync(m, finalizado)).Select(p => p.Nome).Should().Equal("Aria", "Fera", "Garra", "Lobo", "Bran");
        (await LogAsync(m, andamento)).Where(l => l.Tipo == "ParticipanteRemovido").Select(l => l.ParticipanteNome)
            .Should().BeEquivalentTo("Aria", "Fera", "Garra");
    }

    /// <summary>Review Focus 1: quem tem a vez em dois encontros em andamento.</summary>
    [Fact]
    public async Task Remover_quem_tem_a_vez_passa_a_vez()
    {
        var m = await MesaAsync();
        var a = await EncontroAsync(m, ("Aria", 20), ("Lobo", 10));
        var b = await EncontroAsync(m, ("Aria", 20), ("Fera", 15), ("Lobo", 10));
        await AcaoAsync(m, a, "start");
        await AcaoAsync(m, b, "start");
        (await EncontroLidoAsync(m, a)).CurrentParticipantId.Should().Be((await ParticipantesAsync(m, a))[0].Id);

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        foreach (var (encontro, removidos) in new[] { (a, new[] { "Aria" }), (b, new[] { "Aria", "Fera" }) })
        {
            var lobo = (await ParticipantesAsync(m, encontro)).Should().ContainSingle().Subject;
            lobo.Nome.Should().Be("Lobo");
            lobo.AcoesRestantes.Should().Be(3);
            var lido = await EncontroLidoAsync(m, encontro);
            lido.CurrentParticipantId.Should().Be(lobo.Id);
            lido.CurrentRound.Should().Be(1);
            var log = await LogAsync(m, encontro);
            log.Where(l => l.Tipo == "ParticipanteRemovido").Select(l => l.ParticipanteNome).Should().BeEquivalentTo(removidos);
            log.Last().Tipo.Should().Be("VezPassada");
            log.Last().ParticipanteNome.Should().Be("Lobo");
        }
    }

    [Fact]
    public async Task Notas_secretas_ficam()
    {
        var m = await MesaAsync();
        var nota = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{m.CampaignId}/secret-notes", m.Gm,
            new CreateSecretNoteRequest("Pista", [m.JogadorId], []))).Content.ReadFromJsonAsync<SecretNoteResponse>())!;

        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var doGm = (await LerAsync<List<SecretNoteResponse>>(m.Gm, $"/api/campaigns/{m.CampaignId}/secret-notes")).Should().ContainSingle().Subject;
        doGm.Id.Should().Be(nota.Id);
        doGm.RecipientUserIds.Should().Equal(m.JogadorId);
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/campaigns/{m.CampaignId}/secret-notes", m.Jogador))).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await AdicionarMembroAsync(m.Gm, m.CampaignId, m.JogadorId);
        (await LerAsync<List<SecretNoteResponse>>(m.Jogador, $"/api/campaigns/{m.CampaignId}/secret-notes")).Select(n => n.Id).Should().Equal(nota.Id);
    }

    [Fact]
    public async Task Readicionar_tira_do_cemiterio_e_nao_devolve_concessoes()
    {
        var m = await MesaAsync();
        (await RemoverAsync(m.Gm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        await AdicionarMembroAsync(m.Gm, m.CampaignId, m.JogadorId);

        (await LerAsync<CharacterSheetResponse>(m.Jogador, $"/api/character-sheets/{m.Aria}")).NoCemiterio.Should().BeFalse();
        (await DonosAsync(m)).Should().Be((null, null));
        (await Client.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/npc-sheets/{m.Fera}", m.Jogador))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Outro_GM_recebe_404()
    {
        var m = await MesaAsync();
        var outroGm = await RegisterGmAndGetTokenAsync(Unique("RmGm"), Unique("rmgm") + "@t.com");

        (await RemoverAsync(outroGm, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await LerAsync<List<CampaignMemberResponse>>(m.Gm, $"/api/campaigns/{m.CampaignId}/members")).Should().HaveCount(2);
    }

    [Fact]
    public async Task Nao_membro_recebe_404()
    {
        var m = await MesaAsync();
        var (foraId, _) = await RegisterJogadorLinkedToAsync(m.Gm, Unique("RmFo"), Unique("rmfo") + "@t.com");

        (await RemoverAsync(m.Gm, m.CampaignId, foraId)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await RemoverAsync(m.Gm, m.CampaignId, Guid.NewGuid().ToString())).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Jogador_nao_remove()
    {
        var m = await MesaAsync();

        (await RemoverAsync(m.Jogador, m.CampaignId, m.JogadorId)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await LerAsync<List<CampaignMemberResponse>>(m.Gm, $"/api/campaigns/{m.CampaignId}/members")).Should().HaveCount(2);
    }
}
