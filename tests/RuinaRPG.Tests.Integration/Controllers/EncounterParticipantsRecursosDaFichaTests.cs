using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Contracts.SheetLogs;
using RuinaRPG.Domain.Encounters;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// O GM edita no encontro PV/PF/PA de um participante ligado a uma ficha (Personagem, NPC/Criatura concedida): o
/// servidor grava o Atual da ficha, no total que ela mostra com as Condições ativas, só no recurso que o GM mudou; o
/// log da ficha registra pelo GM, o encontro ganha uma linha por recurso, e o dono e o GM recebem CondicoesAlteradas.
/// Toda ficha daqui tem Vigor 8 e Astúcia 7: totais Vitalidade 16, Foco 14, Adrenalina 10.
/// </summary>
public class EncounterParticipantsRecursosDaFichaTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Personagem = "character-sheets";
    private const string Npc = "npc-sheets";
    private const string Criatura = "creature-sheets";

    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaysSilent = TimeSpan.FromSeconds(1);

    private sealed record Mesa(string Gm, string Jogador, string JogadorId, string Outro, string CampaignId, string Tag);

    private sealed record Ficha(string Rota, string Id)
    {
        public string Kind => Rota switch { Personagem => "character", Npc => "npc", _ => "creature" };
    }

    private sealed record Recursos(int Vitalidade, int Foco, int Adrenalina);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null, HttpStatusCode? esperado = null)
    {
        var response = await Client.SendAsync(Authed(method, url, token, body));
        if (esperado is { } status)
            response.StatusCode.Should().Be(status, $"{method} {url}: {await response.Content.ReadAsStringAsync()}");
        else
            response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} devolveu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<T> LerAsync<T>(string token, string url) =>
        (await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadFromJsonAsync<T>())!;

    private async Task<string> GmAsync(bool auditor = false)
    {
        var nick = Unique("RfGm");
        var gm = (await (await Client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        if (auditor)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Users.SingleAsync(u => u.Nickname == nick)).IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
        }
        return gm;
    }

    private async Task<(string Id, string Token)> JogadorAsync(string gm)
    {
        var code = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var nick = Unique("RfJg");
        var tokens = (await (await Client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123", code)))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        return ((await LerAsync<MeResponse>(tokens.AccessToken, "/api/auth/me")).Id, tokens.AccessToken);
    }

    private async Task<Mesa> MesaAsync(string tag)
    {
        var gm = await GmAsync(auditor: true);
        var (jogadorId, jogador) = await JogadorAsync(gm);
        var (outroId, outro) = await JogadorAsync(gm);
        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        foreach (var id in new[] { jogadorId, outroId })
            await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(id));
        return new Mesa(gm, jogador, jogadorId, outro, campaignId, tag);
    }

    /// <summary>Personagem do jogador, ou NPC/Criatura concedida a ele.</summary>
    private async Task<Ficha> NovaFichaAsync(Mesa mesa, string rota)
    {
        var id = rota == Personagem
            ? (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
                .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id
            : (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/grants", mesa.Gm, new GrantSheetRequest(mesa.JogadorId, rota == Npc ? "Npc" : "Creature", null)))
                .Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        foreach (var (atributo, total) in new[] { ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });
        return new Ficha(rota, id);
    }

    /// <summary>Grava o Atual direto no banco, fora de uma requisição (não entra em log nenhum).</summary>
    private async Task GuardarAtualAsync(Ficha f, Recursos r)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(f.Id);
        switch (f.Rota)
        {
            case Personagem:
                var p = (await db.CharacterSheets.FindAsync(id))!;
                (p.VitalidadeAtual, p.FocoAtual, p.AdrenalinaAtual) = (r.Vitalidade, r.Foco, r.Adrenalina);
                break;
            case Npc:
                var n = (await db.NpcSheets.FindAsync(id))!;
                (n.VitalidadeAtual, n.FocoAtual, n.AdrenalinaAtual) = (r.Vitalidade, r.Foco, r.Adrenalina);
                break;
            default:
                var c = (await db.CreatureSheets.FindAsync(id))!;
                (c.VitalidadeAtual, c.FocoAtual, c.AdrenalinaAtual) = (r.Vitalidade, r.Foco, r.Adrenalina);
                break;
        }
        await db.SaveChangesAsync();
    }

    private async Task<(Recursos Atual, Recursos Maximo)> RecursosAsync(Mesa mesa, Ficha f)
    {
        var json = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Get, $"/api/{f.Rota}/{f.Id}", mesa.Gm, esperado: HttpStatusCode.OK)).Content.ReadAsStreamAsync())).RootElement;
        return (new Recursos(json.GetProperty("vitalidadeAtual").GetInt32(), json.GetProperty("focoAtual").GetInt32(), json.GetProperty("adrenalinaAtual").GetInt32()),
            new Recursos(json.GetProperty("vitalidadeMaximo").GetInt32(), json.GetProperty("focoMaximo").GetInt32(), json.GetProperty("adrenalinaMaximo").GetInt32()));
    }

    private async Task<string> CondicaoAsync(Mesa mesa, string recurso, int valor) =>
        (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, new SalvarCondicaoRequest($"Lenta {mesa.Tag}", "Descrição.", "Veneno", null,
            [new CondicaoModificadorDto("RecursoTotal", recurso, "Fixo", valor, null)], []), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;

    private async Task<(Mesa Mesa, Ficha Ficha, string EncounterId, string ParticipantId)> CenaAsync(string tag, string rota = Personagem, bool iniciar = true)
    {
        var mesa = await MesaAsync(tag);
        var ficha = await NovaFichaAsync(mesa, rota);
        var encounterId = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro")))
            .Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
        var request = new AddParticipantRequest(rota == Personagem ? ficha.Id : null, rota == Npc ? ficha.Id : null, rota == Criatura ? ficha.Id : null, 10);
        var participantId = (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request, HttpStatusCode.Created))
            .Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
        if (iniciar)
            await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", mesa.Gm, esperado: HttpStatusCode.NoContent);
        return (mesa, ficha, encounterId, participantId);
    }

    /// <summary>O PUT do formulário do GM: os valores enviados, com os carregados ao lado.</summary>
    private Task<HttpResponseMessage> EditarAsync(string gm, string encounterId, string participantId, Recursos carregado, Recursos enviado, HttpStatusCode esperado = HttpStatusCode.NoContent) =>
        EnviarAsync(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{participantId}", gm,
            new UpdateParticipantRequest(10, enviado.Vitalidade, enviado.Foco, enviado.Adrenalina, 3, [], carregado.Vitalidade, carregado.Foco, carregado.Adrenalina), esperado);

    private async Task<List<SheetLogEntryResponse>> LogDaFichaAsync(Mesa mesa, Ficha f) =>
        (await LerAsync<SheetLogPageResponse>(mesa.Gm, $"/api/sheet-logs/{f.Kind}/{f.Id}")).Itens;

    private async Task<List<RuinaRPG.Infrastructure.Encounters.EncounterLogEntry>> LogDoEncontroAsync(string encounterId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(encounterId);
        return await db.EncounterLogEntries.AsNoTracking().Where(l => l.EncounterId == id).ToListAsync();
    }

    private async Task<HubConnection> ConectarAsync(string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    private sealed class Caixa
    {
        private readonly ConcurrentQueue<CondicoesAlteradasNotification> _recebidas = new();
        private readonly SemaphoreSlim _chegou = new(0);

        public Caixa(HubConnection connection) =>
            connection.On<CondicoesAlteradasNotification>(NotificationEvents.CondicoesAlteradas, n => { _recebidas.Enqueue(n); _chegou.Release(); });

        public List<CondicoesAlteradasNotification> Recebidas => _recebidas.ToList();

        public Task<bool> EsperarAsync(TimeSpan prazo) => _chegou.WaitAsync(prazo);
    }

    [Fact]
    public async Task O_PV_editado_vai_para_a_ficha_no_total_com_as_Condicoes_com_log_pelo_GM_e_uma_linha_no_encontro()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Pv");
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", mesa.Gm,
            new AplicarCondicaoRequest(await CondicaoAsync(mesa, "Vitalidade", -6), null, null, null), HttpStatusCode.Created);
        await GuardarAtualAsync(ficha, new Recursos(4, 14, 10));
        var logAntes = (await LogDoEncontroAsync(encounterId)).Count;

        // Acima do total com a Condição (16 - 6 = 10): fica no total.
        await EditarAsync(mesa.Gm, encounterId, participantId, carregado: new Recursos(4, 14, 10), enviado: new Recursos(30, 14, 10));

        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(10, 14, 10), new Recursos(10, 14, 10)));
        var participante = (await LerAsync<List<EncounterParticipantResponse>>(mesa.Gm, $"/api/encounters/{encounterId}/participants")).Single();
        participante.PV.Should().Be(10);
        (await LogDaFichaAsync(mesa, ficha)).Should().ContainSingle(l => l.Campo == "Vitalidade" && l.ValorAntes == "4")
            .Which.Should().Match<SheetLogEntryResponse>(l => l.ActorIsGm && !l.Automatico && l.ValorDepois == "10");
        var novas = (await LogDoEncontroAsync(encounterId)).OrderBy(l => l.Ordem).Skip(logAntes).ToList();
        novas.Should().ContainSingle().Which.Should().Match<RuinaRPG.Infrastructure.Encounters.EncounterLogEntry>(l =>
            l.Tipo == EncounterLogTipo.RecursoDaFicha && l.Campo == "Vitalidade" && l.ValorAntes == "4" && l.ValorDepois == "10");

        // Abaixo do total: gravado como veio.
        await EditarAsync(mesa.Gm, encounterId, participantId, carregado: new Recursos(10, 14, 10), enviado: new Recursos(7, 14, 10));
        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(7, 14, 10));
    }

    [Fact]
    public async Task Na_preparacao_a_ficha_muda_e_o_log_da_ficha_registra_mas_o_do_encontro_nao()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Prep", iniciar: false);
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));

        await EditarAsync(mesa.Gm, encounterId, participantId, carregado: new Recursos(16, 14, 10), enviado: new Recursos(11, 14, 10));

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(11, 14, 10));
        (await LogDaFichaAsync(mesa, ficha)).Should().ContainSingle(l => l.Campo == "Vitalidade" && l.ActorIsGm && l.ValorDepois == "11");
        (await LogDoEncontroAsync(encounterId)).Should().BeEmpty("o log do encontro só registra com ele em andamento (R0010)");
    }

    [Fact]
    public async Task Na_instancia_do_GM_um_formulario_desatualizado_mantem_o_tique_e_a_edicao_de_fato_salva()
    {
        var mesa = await MesaAsync("Inst");
        var npcId = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", mesa.Gm)).Content.ReadAsStreamAsync())).RootElement.GetProperty("id").GetString()!;
        var encounterId = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro")))
            .Content.ReadFromJsonAsync<EncounterResponse>())!.Id;
        var participantId = (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, new AddParticipantRequest(null, npcId, null, 10), HttpStatusCode.Created))
            .Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
        async Task GuardarNaInstanciaAsync(Recursos r)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var p = (await db.EncounterParticipants.FindAsync(Guid.Parse(participantId)))!;
            (p.PVAtual, p.PFAtual, p.PAAtual) = (r.Vitalidade, r.Foco, r.Adrenalina);
            await db.SaveChangesAsync();
        }
        async Task<Recursos> DaInstanciaAsync()
        {
            var p = (await LerAsync<List<EncounterParticipantResponse>>(mesa.Gm, $"/api/encounters/{encounterId}/participants")).Single();
            return new Recursos(p.PV!.Value, p.PF!.Value, p.PA!.Value);
        }
        await GuardarNaInstanciaAsync(new Recursos(10, 6, 4));
        var carregado = new Recursos(10, 6, 4);

        // Um tique de veneno com o formulário do GM aberto; ele só muda a Iniciativa e salva o formulário de antes.
        await GuardarNaInstanciaAsync(new Recursos(7, 6, 4));
        await EditarAsync(mesa.Gm, encounterId, participantId, carregado, enviado: carregado);
        (await DaInstanciaAsync()).Should().Be(new Recursos(7, 6, 4));

        // Uma edição de fato é gravada (na instância, sem teto, como antes).
        await EditarAsync(mesa.Gm, encounterId, participantId, carregado: new Recursos(7, 6, 4), enviado: new Recursos(30, 6, 1));
        (await DaInstanciaAsync()).Should().Be(new Recursos(30, 6, 1));

        // Cliente antigo, sem os carregados: o formulário inteiro é gravado.
        await EnviarAsync(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm,
            new UpdateParticipantRequest(10, 2, 3, 4, 3, []), HttpStatusCode.NoContent);
        (await DaInstanciaAsync()).Should().Be(new Recursos(2, 3, 4));
    }

    [Fact]
    public async Task O_recurso_que_o_GM_nao_mudou_nao_e_gravado_e_a_mudanca_do_jogador_fica()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Conc");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));
        var carregado = new Recursos(16, 14, 10);
        // O jogador muda Foco e Adrenalina enquanto o formulário do GM está aberto.
        await GuardarAtualAsync(ficha, new Recursos(16, 5, 2));

        await EditarAsync(mesa.Gm, encounterId, participantId, carregado, enviado: new Recursos(12, 14, 10));

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(12, 5, 2));
    }

    [Fact]
    public async Task Sem_os_recursos_o_PUT_nao_mexe_na_ficha()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Nulo");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));

        await EnviarAsync(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm,
            new UpdateParticipantRequest(12, null, null, null, 2, []), HttpStatusCode.NoContent);

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(16, 14, 10));
    }

    [Theory]
    [InlineData(Npc, "Npc", "Foco (PF)")]
    [InlineData(Criatura, "Cri", "Arcana (PF)")]
    public async Task NPC_e_Criatura_concedidos_tambem_mudam_na_ficha(string rota, string tag, string rotuloDoFoco)
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync($"Conc{tag}", rota);
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));

        await EditarAsync(mesa.Gm, encounterId, participantId, carregado: new Recursos(16, 14, 10), enviado: new Recursos(999, 3, 10));

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(16, 3, 10));
        (await LogDaFichaAsync(mesa, ficha)).Should().ContainSingle(l => l.Campo == rotuloDoFoco)
            .Which.Should().Match<SheetLogEntryResponse>(l => l.ActorIsGm && l.ValorAntes == "14" && l.ValorDepois == "3");
        (await LogDoEncontroAsync(encounterId)).Where(l => l.Tipo == EncounterLogTipo.RecursoDaFicha).Should().ContainSingle(l => l.Campo == rotuloDoFoco);
    }

    [Fact]
    public async Task Outro_GM_recebe_404_e_a_ficha_nao_muda()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Outro");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));
        var outroGm = await GmAsync();

        await EditarAsync(outroGm, encounterId, participantId, new Recursos(16, 14, 10), new Recursos(1, 1, 1), HttpStatusCode.NotFound);

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(16, 14, 10));
    }

    [Fact]
    public async Task O_dono_e_o_GM_recebem_CondicoesAlteradas_e_outro_jogador_nada()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Aviso");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10));
        await using var dono = await ConectarAsync(mesa.Jogador);
        await using var gm = await ConectarAsync(mesa.Gm);
        await using var outro = await ConectarAsync(mesa.Outro);
        var (caixaDoDono, caixaDoGm, caixaDoOutro) = (new Caixa(dono), new Caixa(gm), new Caixa(outro));

        // Nada mudou na ficha: ninguém é avisado.
        await EditarAsync(mesa.Gm, encounterId, participantId, new Recursos(16, 14, 10), new Recursos(16, 14, 10));
        (await caixaDoDono.EsperarAsync(StaysSilent)).Should().BeFalse();

        await EditarAsync(mesa.Gm, encounterId, participantId, new Recursos(16, 14, 10), new Recursos(9, 14, 10));

        (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue();
        (await caixaDoGm.EsperarAsync(Arrives)).Should().BeTrue();
        await Task.Delay(StaysSilent);
        var esperada = new CondicoesAlteradasNotification("character", ficha.Id);
        caixaDoDono.Recebidas.Should().ContainSingle().Which.Should().Be(esperada);
        caixaDoGm.Recebidas.Should().ContainSingle().Which.Should().Be(esperada);
        caixaDoOutro.Recebidas.Should().BeEmpty();
    }
}
