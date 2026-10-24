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
/// O lado do tempo das Condições no encontro: linhas de valor atual "ao aplicar" (uma vez, na primeira vez em que a
/// Condição está ativa) e "a cada vez" (cada vez que o participante recebe a vez, uma vez por Turno), e a expiração
/// das que têm duração em Turnos. Toda ficha daqui tem Vigor 8 e Astúcia 7: totais Vitalidade 16, Foco 14,
/// Adrenalina 10, Estresse 10. "Aria" é a Ficha de Personagem do jogador; "Lobo" é um NPC do GM (instância no encontro).
/// </summary>
public class CondicoesNoTurnoTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Personagem = "character-sheets";
    private const string Npc = "npc-sheets";

    // ---------- infraestrutura ----------

    private sealed record Mesa(string Gm, string JogadorId, string CampaignId, string Tag);

    private sealed record Ficha(string Rota, string Id)
    {
        public string Kind => Rota == Personagem ? "character" : "npc";
    }

    private sealed record Recursos(int Vitalidade, int Foco, int Adrenalina, int Estresse);

    /// <summary>Aria (Personagem) e Lobo (instância de um NPC do GM) num encontro.</summary>
    private sealed record Cena(Mesa Mesa, Ficha Aria, Ficha FichaDoLobo, string EncounterId, string AriaId, string LoboId);

    private static readonly Recursos Cheios = new(16, 14, 10, 10);

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
        var registro = await Client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"TnGm{tag}", $"tngm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"TNGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
        }

        var codigo = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var jogadorRegistro = await Client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"TnJg{tag}", $"tnjg{tag}@teste.com", "Senha!123", "Senha!123", codigo));
        var jogador = (await jogadorRegistro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var jogadorId = (await LerAsync<MeResponse>(jogador, "/api/auth/me")).Id;

        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(jogadorId));
        return new Mesa(gm, jogadorId, campaignId, tag);
    }

    /// <summary>Personagem do jogador ou NPC do GM, com nome e o Atual dado (gravados direto no banco, fora de log).</summary>
    private async Task<Ficha> NovaFichaAsync(Mesa mesa, string rota, string nome, Recursos atual)
    {
        var id = rota == Personagem
            ? (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
                .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id
            : (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Post, $"/api/{rota}", mesa.Gm)).Content.ReadAsStreamAsync())).RootElement.GetProperty("id").GetString()!;

        foreach (var (atributo, total) in new[] { ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });
        var ficha = new Ficha(rota, id);
        await GuardarAsync(ficha, atual, nome);
        return ficha;
    }

    private async Task GuardarAsync(Ficha f, Recursos r, string? nome = null)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(f.Id);
        if (f.Rota == Personagem)
        {
            var p = (await db.CharacterSheets.FindAsync(id))!;
            (p.VitalidadeAtual, p.FocoAtual, p.AdrenalinaAtual, p.EstresseAtual) = (r.Vitalidade, r.Foco, r.Adrenalina, r.Estresse);
            p.Nome = nome ?? p.Nome;
        }
        else
        {
            var n = (await db.NpcSheets.FindAsync(id))!;
            (n.VitalidadeAtual, n.FocoAtual, n.AdrenalinaAtual, n.EstresseAtual) = (r.Vitalidade, r.Foco, r.Adrenalina, r.Estresse);
            n.Nome = nome ?? n.Nome;
        }
        await db.SaveChangesAsync();
    }

    private async Task<(Recursos Atual, Recursos Maximo)> RecursosAsync(Mesa mesa, Ficha f)
    {
        var json = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Get, $"/api/{f.Rota}/{f.Id}", mesa.Gm, esperado: HttpStatusCode.OK)).Content.ReadAsStreamAsync())).RootElement;
        return (new Recursos(json.GetProperty("vitalidadeAtual").GetInt32(), json.GetProperty("focoAtual").GetInt32(), json.GetProperty("adrenalinaAtual").GetInt32(), json.GetProperty("estresseAtual").GetInt32()),
            new Recursos(json.GetProperty("vitalidadeMaximo").GetInt32(), json.GetProperty("focoMaximo").GetInt32(), json.GetProperty("adrenalinaMaximo").GetInt32(), json.GetProperty("estresseMaximo").GetInt32()));
    }

    private async Task<int> VitalidadeAsync(Cena c) => (await RecursosAsync(c.Mesa, c.Aria)).Atual.Vitalidade;

    private static CondicaoModificadorDto Atual(string recurso, int valor, string momento, string tipo = "Fixo") => new("RecursoAtual", recurso, tipo, valor, momento);

    private static CondicaoModificadorDto Total(string recurso, int valor) => new("RecursoTotal", recurso, "Fixo", valor, null);

    private async Task<string> CondicaoAsync(Mesa mesa, string nome, params CondicaoModificadorDto[] linhas) =>
        (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Descrição.", "Veneno", null, [.. linhas], []), HttpStatusCode.Created))
            .Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;

    private async Task<string> AplicarAsync(Cena c, string participantId, string condicaoId, int? duracao = null) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/participants/{participantId}/condicoes", c.Mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, duracao, null, null), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;

    private Task RemoverAsync(Cena c, string participantId, string aplicadaId) =>
        EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{participantId}/condicoes/{aplicadaId}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

    private Task AcaoAsync(Cena c, string acao) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/{acao}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

    private Task ProximaAsync(Cena c) => AcaoAsync(c, "advance-turn");

    private Task AnteriorAsync(Cena c) => AcaoAsync(c, "previous-turn");

    private Task PassarParaAsync(Cena c, string participantId) => AcaoAsync(c, $"turn/{participantId}");

    private async Task<string> ParticiparAsync(Mesa mesa, string encounterId, Ficha f, int iniciativa)
    {
        var request = new AddParticipantRequest(f.Rota == Personagem ? f.Id : null, f.Rota == Npc ? f.Id : null, null, iniciativa);
        return (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
    }

    private async Task<string> NovoEncontroAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro"))).Content.ReadFromJsonAsync<EncounterResponse>())!.Id;

    /// <summary>Por padrão Aria (Iniciativa 20) age antes de Lobo (10). Sem <paramref name="iniciar"/>, fica na preparação.</summary>
    private async Task<Cena> CenaAsync(string tag, bool iniciar = true, int iniciativaDoLobo = 10)
    {
        var mesa = await MesaAsync(tag);
        var aria = await NovaFichaAsync(mesa, Personagem, "Aria", Cheios);
        var lobo = await NovaFichaAsync(mesa, Npc, "Lobo", Cheios);
        var encounterId = await NovoEncontroAsync(mesa);
        var ariaId = await ParticiparAsync(mesa, encounterId, aria, 20);
        var loboId = await ParticiparAsync(mesa, encounterId, lobo, iniciativaDoLobo);
        var cena = new Cena(mesa, aria, lobo, encounterId, ariaId, loboId);
        if (iniciar)
            await AcaoAsync(cena, "start");
        return cena;
    }

    private async Task<EncounterParticipantResponse> ParticipanteAsync(Cena c, string participantId) =>
        (await LerAsync<List<EncounterParticipantResponse>>(c.Mesa.Gm, $"/api/encounters/{c.EncounterId}/participants")).Single(p => p.Id == participantId);

    private Task<List<EncounterLogEntryResponse>> LogAsync(Cena c) =>
        LerAsync<List<EncounterLogEntryResponse>>(c.Mesa.Gm, $"/api/encounters/{c.EncounterId}/log");

    private async Task<List<EncounterLogEntryResponse>> AjustesAsync(Cena c) => (await LogAsync(c)).Where(l => l.Tipo == "AjusteDeCondicao").ToList();

    private async Task<List<SheetLogEntryResponse>> LogDaFichaAsync(Mesa mesa, Ficha f) =>
        (await LerAsync<SheetLogPageResponse>(mesa.Gm, $"/api/sheet-logs/{f.Kind}/{f.Id}")).Itens;

    // ---------- "ao aplicar" ----------

    [Fact]
    public async Task Ao_aplicar_com_o_encontro_em_andamento_roda_na_hora_e_uma_vez_so()
    {
        var c = await CenaAsync("AoApl");
        var golpe = await CondicaoAsync(c.Mesa, "Golpe", Atual("Vitalidade", -4, "AoAplicar"));

        await AplicarAsync(c, c.AriaId, golpe);
        (await VitalidadeAsync(c)).Should().Be(12);

        await ProximaAsync(c);
        await ProximaAsync(c); // Aria recebe a vez no Turno 2
        await AnteriorAsync(c);
        await ProximaAsync(c);
        (await VitalidadeAsync(c)).Should().Be(12);

        var ajuste = (await AjustesAsync(c)).Should().ContainSingle().Subject;
        (ajuste.ParticipanteNome, ajuste.Campo, ajuste.ValorAntes, ajuste.ValorDepois, ajuste.Texto, ajuste.Turno)
            .Should().Be(("Aria", "Vitalidade", "16", "12", $"Golpe {c.Mesa.Tag}", 1));
    }

    [Fact]
    public async Task Ao_aplicar_na_preparacao_roda_ao_iniciar_e_a_removida_antes_de_iniciar_nunca_roda()
    {
        var c = await CenaAsync("AoIni", iniciar: false);
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Golpe", Atual("Vitalidade", -4, "AoAplicar")));
        var tirada = await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Susto", Atual("Foco", -5, "AoAplicar")));
        await RemoverAsync(c, c.AriaId, tirada);
        (await RecursosAsync(c.Mesa, c.Aria)).Atual.Should().Be(Cheios);

        await AcaoAsync(c, "start");
        (await RecursosAsync(c.Mesa, c.Aria)).Atual.Should().Be(Cheios with { Vitalidade = 12 });

        await ProximaAsync(c);
        await ProximaAsync(c);
        await AcaoAsync(c, "finish");
        await AcaoAsync(c, "reopen");
        await ProximaAsync(c);
        (await RecursosAsync(c.Mesa, c.Aria)).Atual.Should().Be(Cheios with { Vitalidade = 12 });
        (await AjustesAsync(c)).Should().ContainSingle(l => l.Turno == 1 && l.ValorAntes == "16" && l.ValorDepois == "12");
    }

    // ---------- "a cada vez" ----------

    [Fact]
    public async Task A_cada_vez_roda_na_Proxima_vez_nao_na_Vez_anterior_e_uma_vez_por_Turno()
    {
        var c = await CenaAsync("ACada");
        // Turno 1, vez de Aria: aplicada agora, só roda quando ela RECEBER a vez.
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -3, "ACadaVez")));
        (await VitalidadeAsync(c)).Should().Be(16);

        await ProximaAsync(c); // Lobo, Turno 1
        (await VitalidadeAsync(c)).Should().Be(16);
        await ProximaAsync(c); // Aria, Turno 2
        (await VitalidadeAsync(c)).Should().Be(13);
        await AnteriorAsync(c); // Lobo, Turno 1: nada roda nem é desfeito
        (await VitalidadeAsync(c)).Should().Be(13);
        await ProximaAsync(c); // Aria, Turno 2 de novo: não repete
        (await VitalidadeAsync(c)).Should().Be(13);
        await ProximaAsync(c); // Lobo, Turno 2
        await ProximaAsync(c); // Aria, Turno 3
        (await VitalidadeAsync(c)).Should().Be(10);

        (await AjustesAsync(c)).Select(l => (l.Turno, l.ParticipanteNome, l.ValorAntes, l.ValorDepois))
            .Should().Equal((2, "Aria", "16", "13"), (3, "Aria", "13", "10"));
    }

    [Fact]
    public async Task Passar_a_vez_para_este_roda_e_repetir_no_mesmo_Turno_nao()
    {
        var c = await CenaAsync("Passa");
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -3, "ACadaVez")));

        await PassarParaAsync(c, c.LoboId);
        (await VitalidadeAsync(c)).Should().Be(16);
        await PassarParaAsync(c, c.AriaId); // Turno 1
        (await VitalidadeAsync(c)).Should().Be(13);
        await PassarParaAsync(c, c.LoboId);
        await PassarParaAsync(c, c.AriaId); // ainda Turno 1
        (await VitalidadeAsync(c)).Should().Be(13);
        await ProximaAsync(c); // Lobo, Turno 1
        await ProximaAsync(c); // Aria, Turno 2
        (await VitalidadeAsync(c)).Should().Be(10);
    }

    [Fact]
    public async Task Iniciar_da_a_vez_ao_primeiro_e_a_linha_a_cada_vez_dele_roda()
    {
        var c = await CenaAsync("IniVez", iniciar: false);
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -3, "ACadaVez")));

        await AcaoAsync(c, "start");

        (await VitalidadeAsync(c)).Should().Be(13);
        (await AjustesAsync(c)).Should().ContainSingle(l => l.Turno == 1 && l.ValorDepois == "13");
    }

    [Fact]
    public async Task Remover_o_participante_da_vez_passa_a_vez_e_quem_recebe_sofre_o_ajuste()
    {
        var c = await CenaAsync("Remov", iniciativaDoLobo: 30); // Lobo tem a vez
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -3, "ACadaVez")));

        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.LoboId}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

        (await VitalidadeAsync(c)).Should().Be(13);
    }

    // ---------- onde o ajuste cai ----------

    [Fact]
    public async Task Na_ficha_o_Atual_fica_entre_0_e_o_total()
    {
        var c = await CenaAsync("Limite");
        await GuardarAsync(c.Aria, Cheios with { Vitalidade = 2, Foco = 12 });

        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Misto", Atual("Vitalidade", -3, "AoAplicar"), Atual("Foco", 5, "AoAplicar")));

        (await RecursosAsync(c.Mesa, c.Aria)).Atual.Should().Be(Cheios with { Vitalidade = 0, Foco = 14 });
        (await AjustesAsync(c)).Select(l => (l.Campo, l.ValorAntes, l.ValorDepois))
            .Should().BeEquivalentTo([("Vitalidade", "2", "0"), ("Foco (PF)", "12", "14")]);
        // A escrita é automática: não vira RecursoDaFicha no encontro.
        (await LogAsync(c)).Should().NotContain(l => l.Tipo == "RecursoDaFicha");
        (await LogDaFichaAsync(c.Mesa, c.Aria)).Should().Contain(l => l.Campo == "Vitalidade" && l.ValorAntes == "2" && l.ValorDepois == "0" && l.Automatico);
    }

    [Fact]
    public async Task Percentual_e_do_total_com_as_Condicoes()
    {
        var c = await CenaAsync("Pct");
        await GuardarAsync(c.Aria, Cheios with { Vitalidade = 10 });

        // Total 16 − 4 = 12; −25% de 12 = −3.
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Ferida", Total("Vitalidade", -4), Atual("Vitalidade", -25, "AoAplicar", "Percentual")));

        (await RecursosAsync(c.Mesa, c.Aria)).Should().Be((Cheios with { Vitalidade = 7 }, Cheios with { Vitalidade = 12 }));
    }

    [Fact]
    public async Task Na_instancia_do_GM_o_ajuste_cai_no_PV_do_participante_e_a_ficha_nao_muda()
    {
        var c = await CenaAsync("Inst");

        // Percentual do total da ficha de origem calculado com as Condições DESTE participante: 16 − 4 = 12, −25% = −3.
        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Ferida", Total("Vitalidade", -4), Atual("Vitalidade", -25, "AoAplicar", "Percentual")));
        (await ParticipanteAsync(c, c.LoboId)).PV.Should().Be(13);

        // Sem teto além do que a instância já aceita hoje; piso 0.
        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Cura", Atual("Vitalidade", 10, "AoAplicar")));
        (await ParticipanteAsync(c, c.LoboId)).PV.Should().Be(23);
        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Queda", Atual("Vitalidade", -30, "AoAplicar"), Atual("Adrenalina", -2, "AoAplicar")));
        var lobo = await ParticipanteAsync(c, c.LoboId);
        (lobo.PV, lobo.PF, lobo.PA).Should().Be((0, 14, 8));

        (await AjustesAsync(c)).Select(l => (l.ParticipanteNome, l.Campo, l.ValorAntes, l.ValorDepois))
            .Should().Equal(("Lobo", "PV", "16", "13"), ("Lobo", "PV", "13", "23"), ("Lobo", "PV", "23", "0"), ("Lobo", "PA", "10", "8"));
        (await RecursosAsync(c.Mesa, c.FichaDoLobo)).Atual.Should().Be(Cheios);
    }

    [Fact]
    public async Task Estresse_na_instancia_do_GM_e_pulado_e_o_log_diz_por_que()
    {
        var c = await CenaAsync("Estr");

        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Pavor", Atual("Estresse", 2, "AoAplicar")));

        var linha = (await AjustesAsync(c)).Should().ContainSingle().Subject;
        (linha.ParticipanteNome, linha.Campo, linha.ValorAntes, linha.ValorDepois).Should().Be(("Lobo", "Estresse", null, null));
        linha.Texto.Should().Be($"Pavor {c.Mesa.Tag}: Estresse não é acompanhado no encontro");
        (await RecursosAsync(c.Mesa, c.FichaDoLobo)).Atual.Should().Be(Cheios);
    }

    [Fact]
    public async Task Sem_a_ficha_de_origem_o_percentual_e_pulado_e_o_fixo_roda()
    {
        var c = await CenaAsync("SemOrig");
        await EnviarAsync(HttpMethod.Delete, $"/api/{Npc}/{c.FichaDoLobo.Id}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Ferida", Atual("Vitalidade", -50, "AoAplicar", "Percentual"), Atual("Foco", -2, "AoAplicar")));

        var lobo = await ParticipanteAsync(c, c.LoboId);
        (lobo.PV, lobo.PF).Should().Be((16, 12));
        (await AjustesAsync(c)).Select(l => (l.Campo, l.ValorAntes, l.ValorDepois, l.Texto)).Should().Equal(
            ("PV", null, null, $"Ferida {c.Mesa.Tag}: ficha de origem removida"),
            ("PF", "14", "12", $"Ferida {c.Mesa.Tag}"));
    }

    // ---------- expiração ----------

    [Fact]
    public async Task Expira_ao_entrar_no_Turno_limite_na_preparacao_e_no_meio_da_luta_e_Vez_anterior_nao_a_traz_de_volta()
    {
        var c = await CenaAsync("Expira", iniciar: false);
        // Aplicada na preparação com 2 Turnos: vale nos Turnos 1 e 2, sai ao entrar no 3.
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Vitalidade", -4)), duracao: 2);
        await AcaoAsync(c, "start");
        await ProximaAsync(c);
        await ProximaAsync(c); // Aria, Turno 2
        (await RecursosAsync(c.Mesa, c.Aria)).Maximo.Vitalidade.Should().Be(12);
        // Aplicada no Turno 2 com 1 Turno: sai ao entrar no 3.
        await AplicarAsync(c, c.LoboId, await CondicaoAsync(c.Mesa, "Tonto", Total("Foco", -1)), duracao: 1);
        await ProximaAsync(c); // Lobo, Turno 2
        (await ParticipanteAsync(c, c.AriaId)).CondicoesAplicadas.Should().ContainSingle(a => a.Ativa);
        (await ParticipanteAsync(c, c.LoboId)).CondicoesAplicadas.Should().ContainSingle(a => a.Ativa);

        await ProximaAsync(c); // Aria, Turno 3

        (await ParticipanteAsync(c, c.AriaId)).CondicoesAplicadas.Should().BeEmpty();
        (await ParticipanteAsync(c, c.LoboId)).CondicoesAplicadas.Should().BeEmpty();
        (await RecursosAsync(c.Mesa, c.Aria)).Maximo.Should().Be(Cheios);
        (await LogAsync(c)).Where(l => l.Tipo == "CondicaoExpirada").Select(l => (l.Turno, l.ParticipanteNome, l.Texto))
            .Should().BeEquivalentTo([(3, "Aria", $"Lenta {c.Mesa.Tag}"), (3, "Lobo", $"Tonto {c.Mesa.Tag}")]);
        (await LogDaFichaAsync(c.Mesa, c.Aria)).Should().ContainSingle(l => l.Acao == "Expirou" && l.ItemNome == $"Lenta {c.Mesa.Tag}" && l.Automatico);

        await AnteriorAsync(c); // Lobo, Turno 2
        (await ParticipanteAsync(c, c.AriaId)).CondicoesAplicadas.Should().BeEmpty();
        (await ParticipanteAsync(c, c.LoboId)).CondicoesAplicadas.Should().BeEmpty();
        (await RecursosAsync(c.Mesa, c.Aria)).Maximo.Should().Be(Cheios);
        await ProximaAsync(c); // Aria, Turno 3 de novo: nada a expirar outra vez
        (await LogAsync(c)).Count(l => l.Tipo == "CondicaoExpirada").Should().Be(2);
    }

    [Fact]
    public async Task Num_novo_Turno_expira_primeiro_e_depois_roda_a_cada_vez_com_o_total_ja_restaurado_e_o_log_fica_nessa_ordem()
    {
        var c = await CenaAsync("Ordem");
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Vitalidade", -4)), duracao: 1);
        (await VitalidadeAsync(c)).Should().Be(12); // corte da ativação
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Alento", Atual("Vitalidade", 3, "ACadaVez")));
        await ProximaAsync(c); // Lobo, Turno 1
        var antes = (await LogAsync(c)).Count;

        await ProximaAsync(c); // Aria, Turno 2: Lenta expira (total volta a 16), Alento +3 → 15

        (await RecursosAsync(c.Mesa, c.Aria)).Should().Be((Cheios with { Vitalidade = 15 }, Cheios));
        (await LogAsync(c)).Skip(antes).Select(l => (l.Tipo, l.Turno, l.ParticipanteNome, l.Texto, l.ValorAntes, l.ValorDepois)).Should().Equal(
            ("NovoTurno", 2, null, null, null, null),
            ("VezPassada", 2, "Aria", null, null, null),
            ("CondicaoExpirada", 2, "Aria", $"Lenta {c.Mesa.Tag}", null, null),
            ("AjusteDeCondicao", 2, "Aria", $"Alento {c.Mesa.Tag}", "12", "15"));
    }

    // ---------- concorrência ----------

    [Fact]
    public async Task Duas_Proxima_vez_simultaneas_aplicam_o_ajuste_uma_vez_so()
    {
        var c = await CenaAsync("Corrida");
        var sangria = await CondicaoAsync(c.Mesa, "Sangria", Atual("Vitalidade", -1, "ACadaVez"));
        await AplicarAsync(c, c.AriaId, sangria);
        await AplicarAsync(c, c.LoboId, sangria);

        var sucessos = 0;
        var conflito = false;
        for (var tentativa = 0; tentativa < 15 && !conflito; tentativa++)
        {
            var resultados = await Task.WhenAll(
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/advance-turn", c.Mesa.Gm)),
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/advance-turn", c.Mesa.Gm)));
            resultados.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.NoContent || s == HttpStatusCode.Conflict);
            sucessos += resultados.Count(r => r.StatusCode == HttpStatusCode.NoContent);
            conflito |= resultados.Any(r => r.StatusCode == HttpStatusCode.Conflict);
        }

        conflito.Should().BeTrue("um par simultâneo deve colidir ao menos uma vez em 15 tentativas");
        // Cada Próxima vez que deu certo deu a vez a alguém num Turno novo para ele: exatamente um ajuste.
        (await AjustesAsync(c)).Should().HaveCount(sucessos);
        ((await VitalidadeAsync(c)) + (await ParticipanteAsync(c, c.LoboId)).PV).Should().Be(32 - sucessos);
    }

    [Fact]
    public async Task Duas_Passar_a_vez_simultaneas_para_quem_ja_tem_a_vez_aplicam_o_ajuste_uma_vez_so()
    {
        // Escolher quem já tem a vez não muda Turno nem vez: sem o encontro no UPDATE, nada detectaria a corrida.
        var c = await CenaAsync("CorEsc"); // Aria tem a vez no Turno 1
        var aplicados = 0;
        var conflito = false;
        for (var tentativa = 0; tentativa < 15 && !conflito; tentativa++)
        {
            // Uma Condição nova a cada tentativa: a linha dela ainda não rodou neste Turno.
            var nome = $"Corte{tentativa}";
            await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, nome, Atual("Vitalidade", -1, "ACadaVez")));

            var resultados = await Task.WhenAll(
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/turn/{c.AriaId}", c.Mesa.Gm)),
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/turn/{c.AriaId}", c.Mesa.Gm)));

            resultados.Select(r => r.StatusCode).Should().OnlyContain(s => s == HttpStatusCode.NoContent || s == HttpStatusCode.Conflict);
            resultados.Should().Contain(r => r.StatusCode == HttpStatusCode.NoContent);
            if (resultados.FirstOrDefault(r => r.StatusCode == HttpStatusCode.Conflict) is { } conflitante)
            {
                conflito = true;
                (await conflitante.Content.ReadAsStringAsync()).Should().Contain("O encontro foi alterado por outra ação. Recarregue e tente de novo.");
            }
            (await AjustesAsync(c)).Count(l => l.Texto == $"{nome} {c.Mesa.Tag}").Should().Be(1, $"a linha de {nome} roda uma vez só");
            aplicados++;
        }

        conflito.Should().BeTrue("um par simultâneo deve colidir ao menos uma vez em 15 tentativas");
        (await VitalidadeAsync(c)).Should().Be(16 - aplicados);
    }

    [Fact]
    public async Task Aplicar_com_linha_ao_aplicar_correndo_com_Proxima_vez_e_conferido_contra_o_encontro()
    {
        var c = await CenaAsync("CorApl");
        var sucessos = 0;
        var conflito = false;
        for (var tentativa = 0; tentativa < 15 && !conflito; tentativa++)
        {
            var nome = $"Golpe{tentativa}";
            var condicaoId = await CondicaoAsync(c.Mesa, nome, Atual("Vitalidade", -1, "AoAplicar"));

            var resultados = await Task.WhenAll(
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/participants/{c.LoboId}/condicoes", c.Mesa.Gm,
                    new AplicarCondicaoRequest(condicaoId, null, null, null))),
                Client.SendAsync(Authed(HttpMethod.Post, $"/api/encounters/{c.EncounterId}/advance-turn", c.Mesa.Gm)));

            var (aplicar, avancar) = (resultados[0], resultados[1]);
            aplicar.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.Conflict);
            avancar.StatusCode.Should().BeOneOf(HttpStatusCode.NoContent, HttpStatusCode.Conflict);
            if (resultados.FirstOrDefault(r => r.StatusCode == HttpStatusCode.Conflict) is { } conflitante)
            {
                conflito = true;
                (await conflitante.Content.ReadAsStringAsync()).Should().Contain("O encontro foi alterado por outra ação. Recarregue e tente de novo.");
            }
            var aplicada = aplicar.StatusCode == HttpStatusCode.Created;
            sucessos += aplicada ? 1 : 0;
            // Aplicada: a linha rodou uma vez (na aplicação ou, se não, no varrimento da Próxima vez); recusada: nada dela ficou.
            (await AjustesAsync(c)).Count(l => l.Texto == $"{nome} {c.Mesa.Tag}").Should().Be(aplicada ? 1 : 0);
            (await ParticipanteAsync(c, c.LoboId)).CondicoesAplicadas.Count(a => a.Nome == $"{nome} {c.Mesa.Tag}").Should().Be(aplicada ? 1 : 0);
        }

        conflito.Should().BeTrue("aplicar com o encontro em andamento é conferido contra a versão do encontro");
        (await ParticipanteAsync(c, c.LoboId)).PV.Should().Be(16 - sucessos);
    }

    // ---------- trazidos da revisão da Tarefa 8 ----------

    [Fact]
    public async Task Remover_o_participante_registra_Condicao_removida_no_log_da_ficha()
    {
        var c = await CenaAsync("RemLog");
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Vitalidade", -4)));
        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Tonta", Total("Foco", -1)));

        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{c.EncounterId}/participants/{c.AriaId}", c.Mesa.Gm, esperado: HttpStatusCode.NoContent);

        (await LogDaFichaAsync(c.Mesa, c.Aria)).Where(l => l.Acao == "Removeu" && l.Secao == "Condições").Select(l => (l.ItemNome, l.Automatico, l.ActorIsGm))
            .Should().BeEquivalentTo([($"Lenta {c.Mesa.Tag}", false, true), ($"Tonta {c.Mesa.Tag}", false, true)]);
    }

    [Fact]
    public async Task Ficha_em_dois_encontros_soma_as_Condicoes_de_ambos_no_ajuste_e_ao_finalizar_um()
    {
        var c = await CenaAsync("Dois");
        var outroId = await NovoEncontroAsync(c.Mesa);
        var noOutro = await ParticiparAsync(c.Mesa, outroId, c.Aria, 20);
        var outro = c with { EncounterId = outroId, AriaId = noOutro };
        await AcaoAsync(outro, "start");
        await GuardarAsync(c.Aria, Cheios with { Vitalidade = 5 });

        await AplicarAsync(c, c.AriaId, await CondicaoAsync(c.Mesa, "Lenta", Total("Vitalidade", -4)));
        await AplicarAsync(outro, noOutro, await CondicaoAsync(c.Mesa, "Fraca", Total("Vitalidade", -2), Atual("Vitalidade", 20, "AoAplicar")));

        // +20 limitado ao total com as Condições dos dois encontros: 16 − 4 − 2 = 10.
        (await RecursosAsync(c.Mesa, c.Aria)).Should().Be((Cheios with { Vitalidade = 10 }, Cheios with { Vitalidade = 10 }));

        await AcaoAsync(c, "finish");

        (await RecursosAsync(c.Mesa, c.Aria)).Should().Be((Cheios with { Vitalidade = 10 }, Cheios with { Vitalidade = 14 }));
    }
}
