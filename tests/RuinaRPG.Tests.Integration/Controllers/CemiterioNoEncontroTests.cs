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
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Controllers;

/// <summary>
/// Gerenciador de Encontros R0002 e R0015: uma Ficha de Personagem no cemitério não entra num encontro; a que já era
/// participante fica, só para leitura (nem o GM nem o sistema gravam na ficha), mas a expiração das Condições segue.
/// "Aria" é a Ficha de Personagem do jogador (Vigor 8: Vitalidade 16); "Lobo" é um NPC do GM.
/// </summary>
public class CemiterioNoEncontroTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string RecusaDoEncontro = "Esta ficha está no cemitério e não pode entrar em encontros.";
    private const string RecusaDeEscrita = "Esta ficha está no cemitério e não pode ser editada.";
    private const string MotivoDoPulo = "ficha no cemitério";

    private sealed record Mesa(string Gm, string Jogador, string JogadorId, string CampaignId, string Tag);

    private sealed record Cena(Mesa Mesa, string AriaFichaId, string EncounterId, string AriaId, string LoboId);

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

    private async Task<Mesa> MesaAsync(string tag)
    {
        tag += Guid.NewGuid().ToString("N")[..6];
        var registro = await Client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"CeGm{tag}", $"cegm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"CEGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
        }

        var codigo = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var jogadorRegistro = await Client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"CeJg{tag}", $"cejg{tag}@teste.com", "Senha!123", "Senha!123", codigo));
        var jogador = (await jogadorRegistro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var jogadorId = (await LerAsync<MeResponse>(jogador, "/api/auth/me")).Id;

        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(jogadorId));
        return new Mesa(gm, jogador, jogadorId, campaignId, tag);
    }

    /// <summary>Aria: Vigor 8 e Astúcia 7 (totais 16/14/10/10), com o Atual cheio.</summary>
    private async Task<string> AriaAsync(Mesa mesa)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        foreach (var (atributo, total) in new[] { ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/character-sheets/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var p = (await db.CharacterSheets.FindAsync(Guid.Parse(id)))!;
        (p.VitalidadeAtual, p.FocoAtual, p.AdrenalinaAtual, p.EstresseAtual, p.Nome) = (16, 14, 10, 10, "Aria");
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<string> LoboAsync(Mesa mesa)
    {
        var id = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", mesa.Gm)).Content.ReadAsStreamAsync())).RootElement.GetProperty("id").GetString()!;
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        (await db.NpcSheets.FindAsync(Guid.Parse(id)))!.Nome = "Lobo";
        await db.SaveChangesAsync();
        return id;
    }

    private async Task<string> NovoEncontroAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro"))).Content.ReadFromJsonAsync<EncounterResponse>())!.Id;

    private async Task<string> ParticiparAsync(Mesa mesa, string encounterId, AddParticipantRequest request) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;

    /// <summary>Aria (Iniciativa 20) age antes de Lobo (10). Sem <paramref name="iniciar"/>, fica na preparação.</summary>
    private async Task<Cena> CenaAsync(string tag, bool iniciar = true)
    {
        var mesa = await MesaAsync(tag);
        var aria = await AriaAsync(mesa);
        var lobo = await LoboAsync(mesa);
        var encounterId = await NovoEncontroAsync(mesa);
        var ariaId = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(aria, null, null, 20));
        var loboId = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(null, lobo, null, 10));
        var cena = new Cena(mesa, aria, encounterId, ariaId, loboId);
        if (iniciar)
            await AcaoAsync(cena, "start");
        return cena;
    }

    private Task MatarAsync(Mesa mesa, string fichaId) =>
        EnviarAsync(HttpMethod.Put, $"/api/character-sheets/{fichaId}/morte", mesa.Gm, new DefinirMorteRequest(true), HttpStatusCode.NoContent);

    private Task AcaoAsync(Cena c, string acao) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/{acao}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

    private Task ProximaAsync(Cena c) => AcaoAsync(c, "advance-turn");

    private static CondicaoModificadorDto Atual(string recurso, int valor, string momento) => new("RecursoAtual", recurso, "Fixo", valor, momento);

    private static CondicaoModificadorDto Total(string recurso, int valor) => new("RecursoTotal", recurso, "Fixo", valor, null);

    private async Task<string> CondicaoAsync(Mesa mesa, string nome, params CondicaoModificadorDto[] linhas) =>
        (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Descrição.", "Veneno", null, [.. linhas], []), HttpStatusCode.Created))
            .Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;

    private Task<HttpResponseMessage> AplicarAsync(Cena c, string participantId, string condicaoId, int? duracao = null, HttpStatusCode esperado = HttpStatusCode.Created) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/participants/{participantId}/condicoes", c.Mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, duracao, null, null), esperado);

    private async Task<List<EncounterParticipantResponse>> ParticipantesAsync(Cena c) =>
        await LerAsync<List<EncounterParticipantResponse>>(c.Mesa.Gm, $"/api/encounters/{c.EncounterId}/participants");

    private async Task<EncounterResponse> EncontroAsync(Cena c) =>
        (await LerAsync<List<EncounterResponse>>(c.Mesa.Gm, $"/api/campaigns/{c.Mesa.CampaignId}/encounters")).Single(e => e.Id == c.EncounterId);

    private Task<List<EncounterLogEntryResponse>> LogAsync(Cena c) =>
        LerAsync<List<EncounterLogEntryResponse>>(c.Mesa.Gm, $"/api/encounters/{c.EncounterId}/log");

    private async Task<List<SheetLogEntryResponse>> LogDaFichaAsync(Cena c) =>
        (await LerAsync<SheetLogPageResponse>(c.Mesa.Gm, $"/api/sheet-logs/character/{c.AriaFichaId}")).Itens;

    private async Task<(int Atual, int Maximo)> VitalidadeAsync(Cena c)
    {
        var json = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Get, $"/api/character-sheets/{c.AriaFichaId}", c.Mesa.Gm, esperado: HttpStatusCode.OK))
            .Content.ReadAsStreamAsync())).RootElement;
        return (json.GetProperty("vitalidadeAtual").GetInt32(), json.GetProperty("vitalidadeMaximo").GetInt32());
    }

    [Fact]
    public async Task Ficha_no_cemiterio_nao_entra_no_encontro()
    {
        var mesa = await MesaAsync("Entra");
        var aria = await AriaAsync(mesa);
        var encounterId = await NovoEncontroAsync(mesa);
        await MatarAsync(mesa, aria);

        var resposta = await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm,
            new AddParticipantRequest(aria, null, null, 10), HttpStatusCode.Conflict);

        (await resposta.Content.ReadAsStringAsync()).Should().Be(RecusaDoEncontro);
        (await LerAsync<List<EncounterParticipantResponse>>(mesa.Gm, $"/api/encounters/{encounterId}/participants")).Should().BeEmpty();
    }

    [Fact]
    public async Task Participante_continua_no_encontro_ao_entrar_no_cemiterio()
    {
        var c = await CenaAsync("Fica");
        (await ParticipantesAsync(c)).Should().OnlyContain(p => !p.NoCemiterio);

        await MatarAsync(c.Mesa, c.AriaFichaId);

        var participantes = await ParticipantesAsync(c);
        participantes.Select(p => (p.Id, p.Iniciativa, p.NoCemiterio)).Should().Equal((c.AriaId, 20, true), (c.LoboId, 10, false));
        (await EncontroAsync(c)).CurrentParticipantId.Should().Be(c.AriaId);

        await ProximaAsync(c);
        await ProximaAsync(c);
        var encontro = await EncontroAsync(c);
        (encontro.CurrentRound, encontro.CurrentParticipantId).Should().Be((2, c.AriaId));
    }

    [Fact]
    public async Task GM_nao_edita_recursos_do_participante_no_cemiterio()
    {
        var c = await CenaAsync("Edita");
        await MatarAsync(c.Mesa, c.AriaFichaId);

        var resposta = await EnviarAsync(HttpMethod.Put, $"/api/encounters/{c.EncounterId}/participants/{c.AriaId}", c.Mesa.Gm,
            new UpdateParticipantRequest(20, 5, 14, 10, 3, [], 16, 14, 10), HttpStatusCode.Conflict);

        (await resposta.Content.ReadAsStringAsync()).Should().Be(RecusaDeEscrita);
        (await VitalidadeAsync(c)).Atual.Should().Be(16);
    }

    [Fact]
    public async Task GM_muda_Iniciativa_e_Acoes_mas_nao_remove_condicao_antiga_do_participante_no_cemiterio()
    {
        var c = await CenaAsync("Inic");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            db.EncounterParticipantConditions.Add(new RuinaRPG.Infrastructure.Encounters.EncounterParticipantCondition
                { Id = Guid.NewGuid(), EncounterParticipantId = Guid.Parse(c.AriaId), Texto = "Caído" });
            await db.SaveChangesAsync();
        }
        await MatarAsync(c.Mesa, c.AriaFichaId);
        var url = $"/api/encounters/{c.EncounterId}/participants/{c.AriaId}";

        var remover = await EnviarAsync(HttpMethod.Put, url, c.Mesa.Gm, new UpdateParticipantRequest(20, 16, 14, 10, 3, [], 16, 14, 10), HttpStatusCode.Conflict);
        await EnviarAsync(HttpMethod.Put, url, c.Mesa.Gm, new UpdateParticipantRequest(25, 16, 14, 10, 1, ["Caído"], 16, 14, 10), HttpStatusCode.NoContent);

        (await remover.Content.ReadAsStringAsync()).Should().Be(RecusaDeEscrita);
        var aria = (await ParticipantesAsync(c)).Single(p => p.Id == c.AriaId);
        (aria.Iniciativa, aria.AcoesRestantes, aria.PV).Should().Be((25, 1, 16));
        aria.Condicoes.Should().Equal("Caído");
    }

    [Fact]
    public async Task GM_nao_aplica_nem_remove_condicao_no_participante_no_cemiterio()
    {
        var c = await CenaAsync("Cond");
        var aplicada = (await (await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Foco", -1)))).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;
        var outra = await CondicaoAsync(c.Mesa, "Tonto", Total("Adrenalina", -1));
        await MatarAsync(c.Mesa, c.AriaFichaId);

        var aplicar = await AplicarAsync(c, c.AriaId, outra, esperado: HttpStatusCode.Conflict);
        var remover = await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.AriaId}/condicoes/{aplicada}", c.Mesa.Gm,
            esperado: HttpStatusCode.Conflict);

        (await aplicar.Content.ReadAsStringAsync()).Should().Be(RecusaDeEscrita);
        (await remover.Content.ReadAsStringAsync()).Should().Be(RecusaDeEscrita);
        (await ParticipantesAsync(c)).Single(p => p.Id == c.AriaId).CondicoesAplicadas.Select(a => a.Id).Should().Equal(aplicada);
        // O NPC do GM não é afetado.
        await AplicarAsync(c, c.LoboId, outra);
    }

    [Fact]
    public async Task GM_remove_o_participante_no_cemiterio()
    {
        var c = await CenaAsync("Remove");
        await MatarAsync(c.Mesa, c.AriaFichaId);

        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.AriaId}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

        (await ParticipantesAsync(c)).Select(p => p.Id).Should().Equal(c.LoboId);
    }

    [Fact]
    public async Task Ajuste_de_condicao_e_pulado_com_linha_no_log()
    {
        var c = await CenaAsync("Ajuste");
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -3, "ACadaVez")));
        await MatarAsync(c.Mesa, c.AriaFichaId);

        await ProximaAsync(c); // Lobo, Turno 1
        await ProximaAsync(c); // Aria, Turno 2: a Sangria rodaria aqui

        (await VitalidadeAsync(c)).Atual.Should().Be(16);
        var ajuste = (await LogAsync(c)).Where(l => l.Tipo == "AjusteDeCondicao").Should().ContainSingle().Subject;
        (ajuste.Turno, ajuste.ParticipanteNome, ajuste.ValorAntes, ajuste.ValorDepois).Should().Be((2, "Aria", null, null));
        ajuste.Texto.Should().Be($"Sangria {c.Mesa.Tag}: {MotivoDoPulo}");
    }

    [Fact]
    public async Task Corte_ao_iniciar_e_pulado()
    {
        var c = await CenaAsync("Corte", iniciar: false);
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Ferida", Total("Vitalidade", -4)));
        await MatarAsync(c.Mesa, c.AriaFichaId);
        var automaticasAntes = (await LogDaFichaAsync(c)).Count(l => l.Automatico);

        await AcaoAsync(c, "start");

        (await VitalidadeAsync(c)).Should().Be((16, 12));
        (await LogDaFichaAsync(c)).Count(l => l.Automatico).Should().Be(automaticasAntes);
        var linha = (await LogAsync(c)).Where(l => l.Tipo == "AjusteDeCondicao").Should().ContainSingle().Subject;
        (linha.ParticipanteNome, linha.ValorAntes, linha.ValorDepois).Should().Be(("Aria", null, null));
        linha.Texto.Should().EndWith(MotivoDoPulo);
    }

    [Fact]
    public async Task Corte_sem_mudanca_nao_gera_linha()
    {
        var c = await CenaAsync("SemCorte", iniciar: false);
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Animo", Total("Vitalidade", 4)));
        await MatarAsync(c.Mesa, c.AriaFichaId);

        await AcaoAsync(c, "start");

        (await LogAsync(c)).Should().NotContain(l => l.Tipo == "AjusteDeCondicao");
    }

    [Fact]
    public async Task Expiracao_continua_valendo()
    {
        var c = await CenaAsync("Expira");
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Foco", -1)), duracao: 1); // sai ao entrar no Turno 2
        await MatarAsync(c.Mesa, c.AriaFichaId);
        await ProximaAsync(c); // Lobo, Turno 1

        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(Client.BaseAddress!, "/hubs/notifications"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(c.Mesa.Jogador);
                options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
            })
            .Build();
        await using var _ = connection;
        var recebidas = new ConcurrentQueue<CondicoesAlteradasNotification>();
        var chegou = new SemaphoreSlim(0);
        connection.On<CondicoesAlteradasNotification>(NotificationEvents.CondicoesAlteradas, n => { recebidas.Enqueue(n); chegou.Release(); });
        await connection.StartAsync();

        await ProximaAsync(c); // Aria, Turno 2

        (await ParticipantesAsync(c)).Single(p => p.Id == c.AriaId).CondicoesAplicadas.Should().BeEmpty();
        (await LogDaFichaAsync(c)).Should().ContainSingle(l => l.Acao == "Expirou" && l.ItemNome == $"Lenta {c.Mesa.Tag}" && l.Automatico);
        (await chegou.WaitAsync(TimeSpan.FromSeconds(5))).Should().BeTrue("o dono é avisado da expiração");
        recebidas.Should().Contain(n => n.SheetId == c.AriaFichaId);
    }
}
