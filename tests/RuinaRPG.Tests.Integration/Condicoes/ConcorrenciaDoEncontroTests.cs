using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RuinaRPG.Api.Services;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.NpcSheets;

namespace RuinaRPG.Tests.Integration.Condicoes;

/// <summary>
/// A guarda de concorrência do encontro (xmin) sem corrida de tempo: <see cref="AntesDoSalvamento"/> faz a "outra ação"
/// salvar logo antes do salvamento da requisição, sempre no mesmo ponto. Dois NPCs do GM, "A" (Iniciativa 20) e "B"
/// (10); "A" tem uma Condição "Vitalidade atual −3 a cada vez" e PV 10.
/// </summary>
public class ConcorrenciaDoEncontroTests : IClassFixture<ApiFixture>, IAsyncLifetime
{
    private readonly ApiFixture _fixture;
    private AntesDoSalvamento _gancho = null!;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ConcorrenciaDoEncontroTests(ApiFixture fixture) => _fixture = fixture;

    public Task InitializeAsync()
    {
        _gancho = new AntesDoSalvamento(_fixture.ConnectionString);
        _factory = new AntesDoSalvamento.Factory(_fixture.ConnectionString, _gancho);
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return _factory.DisposeAsync().AsTask();
    }

    private sealed record Cena(string Gm, string EncounterId, string A, string B, string AplicadaId);

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        return await _client.SendAsync(message);
    }

    private async Task<T> LerAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<Cena> CenaAsync(string tag)
    {
        var registro = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"CcGm{tag}", $"ccgm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using (var db = _gancho.NovoContexto())
        {
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"CCGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true;
            await db.SaveChangesAsync();
        }

        var campaignId = (await LerAsync<CampaignResponse>(await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", "")))).Id;
        var encounterId = (await LerAsync<EncounterResponse>(await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/encounters", gm, new CreateEncounterRequest("Encontro")))).Id;
        async Task<string> ParticiparAsync(int iniciativa)
        {
            var npcId = (await LerAsync<NpcSheetResponse>(await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", gm))).Id;
            return (await LerAsync<EncounterParticipantResponse>(await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", gm,
                new AddParticipantRequest(null, npcId, null, iniciativa)))).Id;
        }
        var a = await ParticiparAsync(20);
        var b = await ParticiparAsync(10);

        var condicao = await LerAsync<CondicaoResponse>(await EnviarAsync(HttpMethod.Post, "/api/condicoes", gm, new SalvarCondicaoRequest($"Sangria {tag}", "Descrição.", "Veneno", null,
            [new CondicaoModificadorDto("RecursoAtual", "Vitalidade", "Fixo", -3, "ACadaVez")], [])));
        var aplicadaId = (await LerAsync<CondicaoAplicadaResponse>(await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{a}/condicoes", gm,
            new AplicarCondicaoRequest(condicao.Id, null, null, null)))).Id;
        await using (var db = _gancho.NovoContexto())
        {
            (await db.EncounterParticipants.FindAsync(Guid.Parse(a)))!.PVAtual = 10;
            await db.SaveChangesAsync();
        }
        return new Cena(gm, encounterId, a, b, aplicadaId);
    }

    private async Task<int?> PvDeAAsync(Cena c)
    {
        await using var db = _gancho.NovoContexto();
        return (await db.EncounterParticipants.FindAsync(Guid.Parse(c.A)))!.PVAtual;
    }

    /// <summary>A "outra ação": muda o encontro e salva, como faria qualquer ação que o altera.</summary>
    private static Func<Infrastructure.Persistence.RuinaRpgDbContext, Task> OutraAcaoNoEncontro(Cena c) => async db =>
    {
        var encounter = (await db.Encounters.FindAsync(Guid.Parse(c.EncounterId)))!;
        encounter.Nome += " (renomeado)";
        await db.SaveChangesAsync();
    };

    [Fact]
    public async Task Dois_contextos_que_mudam_o_mesmo_encontro_o_segundo_a_salvar_recebe_conflito()
    {
        var c = await CenaAsync("Ctx");
        await using var primeiro = _gancho.NovoContexto();
        await using var segundo = _gancho.NovoContexto();
        var noPrimeiro = (await primeiro.Encounters.FindAsync(Guid.Parse(c.EncounterId)))!;
        var noSegundo = (await segundo.Encounters.FindAsync(Guid.Parse(c.EncounterId)))!;

        noPrimeiro.CurrentRound = 1;
        await primeiro.SaveChangesAsync();
        noSegundo.Nome = "Outro";

        await segundo.Invoking(db => db.SaveChangesAsync()).Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Proxima_vez_com_o_encontro_alterado_no_meio_responde_409_e_nao_aplica_o_ajuste()
    {
        var c = await CenaAsync("Prox");
        (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/start", c.Gm)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PvDeAAsync(c)).Should().Be(7, "Iniciar dá a vez a A, que sofre a linha");
        (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/advance-turn", c.Gm)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        // B → A abre o Turno 2: a linha de A rodaria de novo.
        _gancho.Armar(OutraAcaoNoEncontro(c));
        var response = await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/advance-turn", c.Gm);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).Should().Contain(ConflitoDeConcorrenciaDoEncontroAttribute.Mensagem);
        (await PvDeAAsync(c)).Should().Be(7);
    }

    // Passar a vez para quem já a tem não muda coluna nenhuma do encontro: sem forçar a linha no UPDATE, o xmin não
    // seria conferido e uma ação simultânea aplicaria a linha "a cada vez" outra vez.
    [Fact]
    public async Task Passar_a_vez_para_quem_ja_a_tem_com_o_encontro_alterado_no_meio_responde_409_e_nao_aplica_o_ajuste()
    {
        var c = await CenaAsync("Atual");
        (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/start", c.Gm)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await using (var db = _gancho.NovoContexto())
        {
            // A já recebeu a vez neste Turno; para a escolha rodar a linha, o registro do Turno sai.
            var aplicada = await db.EncounterParticipantCondicoes.SingleAsync(x => x.Id == Guid.Parse(c.AplicadaId));
            aplicada.AjustesAplicados = [];
            await db.SaveChangesAsync();
        }

        _gancho.Armar(OutraAcaoNoEncontro(c));
        var response = await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/turn/{c.A}", c.Gm);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await PvDeAAsync(c)).Should().Be(7, "o ajuste desta escolha não foi salvo");

        // Sem a outra ação, a mesma escolha roda a linha.
        (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/turn/{c.A}", c.Gm)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PvDeAAsync(c)).Should().Be(4);
    }

    [Fact]
    public async Task Remover_uma_Condicao_que_outra_acao_acabou_de_remover_responde_404()
    {
        var c = await CenaAsync("Rem");
        _gancho.Armar(async db =>
        {
            db.EncounterParticipantCondicoes.Remove(await db.EncounterParticipantCondicoes.SingleAsync(x => x.Id == Guid.Parse(c.AplicadaId)));
            await db.SaveChangesAsync();
        });

        var response = await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.A}/condicoes/{c.AplicadaId}", c.Gm);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.A}/condicoes/{c.AplicadaId}", c.Gm)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
