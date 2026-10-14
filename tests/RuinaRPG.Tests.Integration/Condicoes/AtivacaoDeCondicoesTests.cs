using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Contracts.SheetLogs;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Condicoes;

/// <summary>
/// O que acontece quando o conjunto de Condições ativas de um participante muda (aplicar, remover, iniciar,
/// finalizar, reabrir, remover o participante): o Atual guardado acima do novo total é cortado de vez, o log da
/// ficha registra a Condição (pelo GM) e o corte (automático), e o corte não vira linha RecursoDaFicha no encontro.
/// Toda ficha daqui tem Vigor 8 e Astúcia 7: totais Vitalidade 16, Foco 14, Adrenalina 10, Estresse 10.
/// </summary>
public class AtivacaoDeCondicoesTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Personagem = "character-sheets";
    private const string Npc = "npc-sheets";
    private const string Criatura = "creature-sheets";

    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public AtivacaoDeCondicoesTests(PostgresFixture postgres) => _postgres = postgres;

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

    // ---------- infraestrutura ----------

    private sealed record Mesa(string Gm, string Jogador, string JogadorId, string CampaignId, string Tag);

    private sealed record Ficha(string Rota, string Id)
    {
        public string Kind => Rota switch { Personagem => "character", Npc => "npc", _ => "creature" };
    }

    private sealed record Recursos(int Vitalidade, int Foco, int Adrenalina, int? Estresse);

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
        var response = await _client.SendAsync(Authed(method, url, token, body));
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
        var registro = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"AtGm{tag}", $"atgm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"ATGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
        }

        var codigo = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var jogadorRegistro = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"AtJg{tag}", $"atjg{tag}@teste.com", "Senha!123", "Senha!123", codigo));
        var jogador = (await jogadorRegistro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var jogadorId = (await LerAsync<MeResponse>(jogador, "/api/auth/me")).Id;

        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(jogadorId));
        return new Mesa(gm, jogador, jogadorId, campaignId, tag);
    }

    /// <summary>Personagem do jogador, ou NPC/Criatura do GM (<paramref name="concedida"/> = false) ou concedida ao jogador.</summary>
    private async Task<Ficha> NovaFichaAsync(Mesa mesa, string rota, bool concedida = true)
    {
        string id;
        if (rota == Personagem)
            id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
                .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        else if (concedida)
            id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/grants", mesa.Gm, new GrantSheetRequest(mesa.JogadorId, rota == Npc ? "Npc" : "Creature", null)))
                .Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        else
            id = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Post, $"/api/{rota}", mesa.Gm)).Content.ReadAsStreamAsync())).RootElement.GetProperty("id").GetString()!;

        foreach (var (atributo, total) in new[] { ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });
        return new Ficha(rota, id);
    }

    /// <summary>Grava o Atual direto no banco, fora de uma requisição (não entra em log nenhum).</summary>
    private async Task GuardarAtualAsync(Ficha f, Recursos r)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(f.Id);
        switch (f.Rota)
        {
            case Personagem:
                var p = (await db.CharacterSheets.FindAsync(id))!;
                (p.VitalidadeAtual, p.FocoAtual, p.AdrenalinaAtual, p.EstresseAtual) = (r.Vitalidade, r.Foco, r.Adrenalina, r.Estresse!.Value);
                break;
            case Npc:
                var n = (await db.NpcSheets.FindAsync(id))!;
                (n.VitalidadeAtual, n.FocoAtual, n.AdrenalinaAtual, n.EstresseAtual) = (r.Vitalidade, r.Foco, r.Adrenalina, r.Estresse!.Value);
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
        int? Opcional(string nome) => json.TryGetProperty(nome, out var v) ? v.GetInt32() : null;
        return (new Recursos(json.GetProperty("vitalidadeAtual").GetInt32(), json.GetProperty("focoAtual").GetInt32(), json.GetProperty("adrenalinaAtual").GetInt32(), Opcional("estresseAtual")),
            new Recursos(json.GetProperty("vitalidadeMaximo").GetInt32(), json.GetProperty("focoMaximo").GetInt32(), json.GetProperty("adrenalinaMaximo").GetInt32(), Opcional("estresseMaximo")));
    }

    private async Task<string> CriarCondicaoAsync(Mesa mesa, string nome, string recurso, int valor)
    {
        var request = new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Descrição.", "Veneno", null,
            [new CondicaoModificadorDto("RecursoTotal", recurso, "Fixo", valor, null)], []);
        return (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;
    }

    private async Task<string> NovoEncontroAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro"))).Content.ReadFromJsonAsync<EncounterResponse>())!.Id;

    private async Task<string> ParticiparAsync(Mesa mesa, string encounterId, Ficha f)
    {
        var request = new AddParticipantRequest(f.Rota == Personagem ? f.Id : null, f.Rota == Npc ? f.Id : null, f.Rota == Criatura ? f.Id : null, 10);
        return (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
    }

    private Task EncontroAsync(Mesa mesa, string encounterId, string acao) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/{acao}", mesa.Gm, esperado: HttpStatusCode.NoContent);

    private async Task<string> AplicarAsync(Mesa mesa, string encounterId, string participantId, string condicaoId) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, null, null, null), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;

    private Task RemoverAsync(Mesa mesa, string encounterId, string participantId, string aplicadaId) =>
        EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicadaId}", mesa.Gm, esperado: HttpStatusCode.NoContent);

    private async Task<(Mesa Mesa, Ficha Ficha, string EncounterId, string ParticipantId)> CenaAsync(string tag, string rota = Personagem, bool iniciar = true, bool concedida = true)
    {
        var mesa = await MesaAsync(tag);
        var ficha = await NovaFichaAsync(mesa, rota, concedida);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        if (iniciar)
            await EncontroAsync(mesa, encounterId, "start");
        return (mesa, ficha, encounterId, participantId);
    }

    private async Task<List<SheetLogEntryResponse>> LogDaFichaAsync(Mesa mesa, Ficha f) =>
        (await LerAsync<SheetLogPageResponse>(mesa.Gm, $"/api/sheet-logs/{f.Kind}/{f.Id}")).Itens;

    // ---------- corte do Atual ----------

    [Fact]
    public async Task Aplicar_menos_10_de_Vitalidade_total_corta_o_Atual_e_remover_nao_devolve()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Corta");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, 10));

        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));

        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(6, 14, 10, 10), new Recursos(6, 14, 10, 10)));

        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);

        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(6, 14, 10, 10), new Recursos(16, 14, 10, 10)));
    }

    [Fact]
    public async Task Atual_abaixo_do_novo_total_nao_muda()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Abaixo");
        await GuardarAtualAsync(ficha, new Recursos(4, 14, 10, 10));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));

        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(4, 14, 10, 10));
        (await LogDaFichaAsync(mesa, ficha)).Should().NotContain(l => l.Campo == "Vitalidade");
    }

    [Fact]
    public async Task Aplicada_na_preparacao_nao_corta_e_Iniciar_corta()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Inicia", iniciar: false);
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, 10));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));
        (await RecursosAsync(mesa, ficha)).Atual.Should().Be(new Recursos(16, 14, 10, 10));

        await EncontroAsync(mesa, encounterId, "start");

        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(6, 14, 10, 10), new Recursos(6, 14, 10, 10)));
    }

    [Fact]
    public async Task Total_que_volta_a_baixar_corta_o_Atual_ao_remover_finalizar_e_remover_o_participante()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Volta");
        var vigor = await CriarCondicaoAsync(mesa, "Vigoroso", "Adrenalina", 5);

        // Remover a Condição: o total volta a 10 e a Adrenalina guardada em 15 desce junto.
        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, vigor);
        await GuardarAtualAsync(ficha, new Recursos(0, 0, 15, 0));
        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);
        (await RecursosAsync(mesa, ficha)).Atual.Adrenalina.Should().Be(10);

        // Finalizar: idem; reabrir devolve o total, não o Atual.
        await AplicarAsync(mesa, encounterId, participantId, vigor);
        await GuardarAtualAsync(ficha, new Recursos(0, 0, 15, 0));
        await EncontroAsync(mesa, encounterId, "finish");
        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(0, 0, 10, 0), new Recursos(16, 14, 10, 10)));
        await EncontroAsync(mesa, encounterId, "reopen");
        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(0, 0, 10, 0), new Recursos(16, 14, 15, 10)));

        // Remover o participante leva as Condições dele junto.
        await GuardarAtualAsync(ficha, new Recursos(0, 0, 15, 0));
        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm, esperado: HttpStatusCode.NoContent);
        (await RecursosAsync(mesa, ficha)).Should().Be((new Recursos(0, 0, 10, 0), new Recursos(16, 14, 10, 10)));
    }

    [Theory]
    [InlineData(Npc, "Npc")]
    [InlineData(Criatura, "Cri")]
    public async Task Npc_e_Criatura_concedidos_tambem_sao_cortados(string rota, string tag)
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync($"Conc{tag}", rota);
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, rota == Npc ? 10 : null));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));

        (await RecursosAsync(mesa, ficha)).Atual.Vitalidade.Should().Be(6);
        (await LogDaFichaAsync(mesa, ficha)).Should().Contain(l => l.Secao == "Condições" && l.ItemNome == $"Sangria {mesa.Tag}")
            .And.Contain(l => l.Automatico && l.ValorAntes == "16" && l.ValorDepois == "6");
    }

    [Theory]
    [InlineData(Npc, "Npc")]
    [InlineData(Criatura, "Cri")]
    public async Task Ficha_do_GM_nao_e_cortada_nem_registrada(string rota, string tag)
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync($"Gm{tag}", rota, concedida: false);
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, rota == Npc ? 10 : null));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));

        (await RecursosAsync(mesa, ficha)).Atual.Vitalidade.Should().Be(16);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(ficha.Id);
        (await db.SheetLogEntries.AnyAsync(l => l.NpcSheetId == id || l.CreatureSheetId == id)).Should().BeFalse();
    }

    // ---------- log da ficha e do encontro ----------

    [Fact]
    public async Task Log_da_ficha_mostra_a_Condicao_pelo_GM_e_o_corte_como_automatico()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("Log");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, 10));
        var nome = $"Sangria {mesa.Tag}";
        var anteriores = (await LogDaFichaAsync(mesa, ficha)).Select(l => l.Id).ToHashSet(); // a preparação da ficha (Atributos)

        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));
        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);

        var log = (await LogDaFichaAsync(mesa, ficha)).Where(l => !anteriores.Contains(l.Id)).ToList();
        log.Should().HaveCount(3);
        var aplicada = log.Single(l => l.Acao == "Adicionou");
        (aplicada.Secao, aplicada.ItemNome, aplicada.ActorNickname, aplicada.ActorIsGm, aplicada.Automatico).Should().Be(("Condições", nome, $"AtGm{mesa.Tag}", true, false));
        var removida = log.Single(l => l.Acao == "Removeu");
        (removida.Secao, removida.ItemNome, removida.ActorIsGm, removida.Automatico).Should().Be(("Condições", nome, true, false));
        var corte = log.Single(l => l.Acao == "Alterou");
        (corte.Campo, corte.ValorAntes, corte.ValorDepois, corte.Categoria, corte.ActorNickname, corte.Automatico)
            .Should().Be(("Vitalidade", "16", "6", "RecursoVFA", $"AtGm{mesa.Tag}", true));
        // O corte entra no mesmo instante da aplicação, que o causou.
        corte.OccurredAt.Should().Be(aplicada.OccurredAt);
    }

    [Fact]
    public async Task Log_do_encontro_nao_ganha_RecursoDaFicha_pelo_corte()
    {
        var (mesa, ficha, encounterId, participantId) = await CenaAsync("SemRec");
        await GuardarAtualAsync(ficha, new Recursos(16, 14, 10, 10));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Sangria", "Vitalidade", -10));

        (await RecursosAsync(mesa, ficha)).Atual.Vitalidade.Should().Be(6);
        var log = await LerAsync<List<EncounterLogEntryResponse>>(mesa.Gm, $"/api/encounters/{encounterId}/log");
        log.Should().Contain(l => l.Tipo == "CondicaoAdicionada");
        log.Should().NotContain(l => l.Tipo == "RecursoDaFicha");
    }
}
