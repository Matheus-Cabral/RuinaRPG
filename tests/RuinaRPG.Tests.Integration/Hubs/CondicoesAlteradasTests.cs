using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Api.Hubs;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.Notifications;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Hubs;

/// <summary>
/// Quando as Condições ativas de um participante ligado a uma ficha mudam, o dono da ficha recebe
/// CondicoesAlteradas pelo NotificationHub e o grupo do encontro recebe ParticipantsChanged. Ninguém mais.
/// </summary>
public class CondicoesAlteradasTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CondicoesAlteradasTests(PostgresFixture postgres) => _postgres = postgres;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory(_postgres.ConnectionString);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private static readonly TimeSpan Arrives = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StaysSilent = TimeSpan.FromSeconds(1);

    private static string Unique(string prefix) => prefix + Guid.NewGuid().ToString("N")[..10];

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return message;
    }

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null, HttpClient? client = null)
    {
        var response = await (client ?? _client).SendAsync(Authed(method, url, token, body));
        response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} devolveu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private sealed record Mesa(string Gm, string Jogador, string JogadorId, string Outro, string CampaignId);

    private async Task<(string Id, string Token)> JogadorAsync(string gm)
    {
        var code = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var nick = Unique("Jg");
        var tokens = (await (await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123", code)))
            .Content.ReadFromJsonAsync<AuthResponse>())!;
        var me = (await (await EnviarAsync(HttpMethod.Get, "/api/auth/me", tokens.AccessToken)).Content.ReadFromJsonAsync<MeResponse>())!;
        return (me.Id, tokens.AccessToken);
    }

    private async Task<Mesa> MesaAsync()
    {
        var nick = Unique("Gm");
        var gm = (await (await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest(nick, $"{nick}@t.com", "Senha!123", "Senha!123")))
            .Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            (await db.Users.SingleAsync(u => u.Nickname == nick)).IsRulesAuditor = true;
            await db.SaveChangesAsync();
        }
        var (jogadorId, jogador) = await JogadorAsync(gm);
        var (outroId, outro) = await JogadorAsync(gm);
        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest("Ruína", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        foreach (var id in new[] { jogadorId, outroId })
            await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(id));
        return new Mesa(gm, jogador, jogadorId, outro, campaignId);
    }

    private async Task<string> CondicaoAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, new SalvarCondicaoRequest(Unique("Lenta"), null, "Veneno", null,
            [new CondicaoModificadorDto("RecursoTotal", "Vitalidade", "Fixo", -2, null)], []))).Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;

    private async Task<string> EncontroAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro"))).Content.ReadFromJsonAsync<EncounterResponse>())!.Id;

    private async Task<string> ParticiparAsync(Mesa mesa, string encounterId, AddParticipantRequest request) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;

    private async Task<string> PersonagemAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;

    private async Task<string> AplicarAsync(Mesa mesa, string encounterId, string participantId, string condicaoId, HttpClient? client = null) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, null, null, null), client)).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;

    private async Task<HubConnection> ConectarAsync(string hub, string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_client.BaseAddress!, hub), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(token);
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
        await connection.StartAsync();
        return connection;
    }

    /// <summary>Guarda cada evento recebido, para o teste conferir quantos chegaram e que nada mais chegou.</summary>
    private sealed class Caixa
    {
        private readonly ConcurrentQueue<CondicoesAlteradasNotification> _recebidas = new();
        private readonly SemaphoreSlim _chegou = new(0);

        public Caixa(HubConnection connection) =>
            connection.On<CondicoesAlteradasNotification>(NotificationEvents.CondicoesAlteradas, n => { _recebidas.Enqueue(n); _chegou.Release(); });

        public Caixa(HubConnection connection, string evento) =>
            connection.On(evento, () => { _recebidas.Enqueue(new("", "")); _chegou.Release(); });

        public List<CondicoesAlteradasNotification> Recebidas => _recebidas.ToList();

        public Task<bool> EsperarAsync(TimeSpan prazo) => _chegou.WaitAsync(prazo);
    }

    [Fact]
    public async Task O_dono_recebe_CondicoesAlteradas_ao_aplicar_remover_iniciar_finalizar_e_reabrir_e_outro_jogador_nada()
    {
        var mesa = await MesaAsync();
        var sheetId = await PersonagemAsync(mesa);
        var encounterId = await EncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(sheetId, null, null, 10));
        var condicaoId = await CondicaoAsync(mesa);
        await using var dono = await ConectarAsync("/hubs/notifications", mesa.Jogador);
        await using var outro = await ConectarAsync("/hubs/notifications", mesa.Outro);
        var caixaDoDono = new Caixa(dono);
        var caixaDoOutro = new Caixa(outro);
        var esperada = new CondicoesAlteradasNotification("character", sheetId);

        // Aplicada na preparação ainda não está ativa: nada muda e ninguém é avisado.
        await AplicarAsync(mesa, encounterId, participantId, condicaoId);
        (await caixaDoDono.EsperarAsync(StaysSilent)).Should().BeFalse();

        foreach (var acao in new[] { "start", "finish", "reopen" })
        {
            await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/{acao}", mesa.Gm);
            (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue($"o dono é avisado em {acao}");
        }

        var aplicadaId = (await (await EnviarAsync(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", mesa.Gm))
            .Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>())!.Single().CondicoesAplicadas.Single().Id;
        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicadaId}", mesa.Gm);
        (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue("o dono é avisado ao remover");

        await AplicarAsync(mesa, encounterId, participantId, condicaoId);
        (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue("o dono é avisado ao aplicar");

        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm);
        (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue("o dono é avisado ao remover o participante");

        await Task.Delay(StaysSilent);
        caixaDoDono.Recebidas.Should().HaveCount(6).And.AllBeEquivalentTo(esperada);
        caixaDoOutro.Recebidas.Should().BeEmpty();
    }

    [Fact]
    public async Task O_dono_de_um_NPC_concedido_recebe_o_tipo_npc_e_a_ficha_do_GM_nao_avisa_ninguem()
    {
        var mesa = await MesaAsync();
        var concedido = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/grants", mesa.Gm, new GrantSheetRequest(mesa.JogadorId, "Npc", null)))
            .Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        var doGm = (await (await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", mesa.Gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.NpcSheets.NpcSheetResponse>())!.Id;
        var encounterId = await EncontroAsync(mesa);
        var participanteConcedido = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(null, concedido, null, 10));
        var participanteDoGm = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(null, doGm, null, 5));
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", mesa.Gm);
        var condicaoId = await CondicaoAsync(mesa);
        await using var dono = await ConectarAsync("/hubs/notifications", mesa.Jogador);
        await using var outro = await ConectarAsync("/hubs/notifications", mesa.Outro);
        var caixaDoDono = new Caixa(dono);
        var caixaDoOutro = new Caixa(outro);

        await AplicarAsync(mesa, encounterId, participanteDoGm, condicaoId);
        (await caixaDoDono.EsperarAsync(StaysSilent)).Should().BeFalse();

        await AplicarAsync(mesa, encounterId, participanteConcedido, condicaoId);
        (await caixaDoDono.EsperarAsync(Arrives)).Should().BeTrue();
        await Task.Delay(StaysSilent);
        caixaDoDono.Recebidas.Should().ContainSingle().Which.Should().Be(new CondicoesAlteradasNotification("npc", concedido));
        caixaDoOutro.Recebidas.Should().BeEmpty();
    }

    [Fact]
    public async Task O_grupo_do_encontro_recebe_ParticipantsChanged()
    {
        var mesa = await MesaAsync();
        var sheetId = await PersonagemAsync(mesa);
        var encounterId = await EncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(sheetId, null, null, 10));
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", mesa.Gm);
        var condicaoId = await CondicaoAsync(mesa);
        await using var gm = await ConectarAsync("/hubs/encounters", mesa.Gm);
        await gm.InvokeAsync("JoinEncounter", encounterId);
        var caixa = new Caixa(gm, "ParticipantsChanged");

        await AplicarAsync(mesa, encounterId, participantId, condicaoId);

        (await caixa.EsperarAsync(Arrives)).Should().BeTrue();
    }

    [Fact]
    public async Task Uma_falha_dos_hubs_nao_derruba_a_acao()
    {
        var mesa = await MesaAsync();
        var sheetId = await PersonagemAsync(mesa);
        var encounterId = await EncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, new AddParticipantRequest(sheetId, null, null, 10));
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", mesa.Gm);
        var condicaoId = await CondicaoAsync(mesa);
        await using var quebrado = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.AddSingleton<IHubContext<NotificationHub>>(new HubQuebrado<NotificationHub>());
            services.AddSingleton<IHubContext<EncounterHub>>(new HubQuebrado<EncounterHub>());
        }));
        using var client = quebrado.CreateClient();

        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, condicaoId, client);
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/finish", mesa.Gm, client: client);

        var participantes = (await (await EnviarAsync(HttpMethod.Get, $"/api/encounters/{encounterId}/participants", mesa.Gm))
            .Content.ReadFromJsonAsync<List<EncounterParticipantResponse>>())!;
        participantes.Single().CondicoesAplicadas.Should().ContainSingle(c => c.Id == aplicadaId);
        var encontro = (await (await EnviarAsync(HttpMethod.Get, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm))
            .Content.ReadFromJsonAsync<List<EncounterResponse>>())!.Single();
        encontro.Status.Should().Be("Finalizado");
    }

    private sealed class HubQuebrado<T> : IHubContext<T> where T : Hub
    {
        public IHubClients Clients => throw new InvalidOperationException("hub down");
        public IGroupManager Groups => throw new InvalidOperationException("hub down");
    }
}
