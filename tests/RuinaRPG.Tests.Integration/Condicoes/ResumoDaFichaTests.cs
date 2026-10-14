using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Auth;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.Encounters;
using RuinaRPG.Domain.Items;
using RuinaRPG.Domain.SpellsAndAbilities;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Condicoes;

/// <summary>
/// GET api/encounters/{id}/participants/{pid}/resumo: os números de um NPC/Criatura com as Condições DAQUELE
/// participante aplicadas. É o único lugar onde uma Condição age sobre uma ficha do GM (sem dono); os endpoints da
/// própria ficha seguem sem mudança. Toda ficha parte de Força 10, Agilidade 6, Vigor 8, Astúcia 7, uma Perícia sobre
/// Força, um Artefato "Anel" (Dano Cortante +10), uma Arma equipada e uma Magia.
/// </summary>
public class ResumoDaFichaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Npc = "npc-sheets";
    private const string Criatura = "creature-sheets";

    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public ResumoDaFichaTests(PostgresFixture postgres) => _postgres = postgres;

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

    private sealed record Mesa(string Gm, Guid GmId, string Jogador, string JogadorId, string CampaignId, string Tag);

    private sealed record Ficha(string Rota, string Id, string Pericia, Guid ArtefatoId);

    private sealed record Linha(string Alvo, string? Especifico, string Tipo, int Valor);

    private static Linha Fixo(string alvo, string? especifico, int valor) => new(alvo, especifico, "Fixo", valor);
    private static Linha Pct(string alvo, string? especifico, int valor) => new(alvo, especifico, "Percentual", valor);

    private async Task<HttpResponseMessage> EnviarAsync(HttpMethod method, string url, string token, object? body = null, HttpStatusCode? esperado = null)
    {
        var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
            message.Content = JsonContent.Create(body);
        var response = await _client.SendAsync(message);
        if (esperado is { } status)
            response.StatusCode.Should().Be(status, $"{method} {url}: {await response.Content.ReadAsStringAsync()}");
        else
            response.IsSuccessStatusCode.Should().BeTrue($"{method} {url} devolveu {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return response;
    }

    private async Task<T> LerAsync<T>(string token, string url) =>
        (await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadFromJsonAsync<T>())!;

    private async Task<string> TextoAsync(string token, string url) =>
        await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadAsStringAsync();

    private async Task<JsonElement> JsonAsync(string token, string url) => JsonDocument.Parse(await TextoAsync(token, url)).RootElement;

    private async Task<Mesa> MesaAsync(string tag)
    {
        var registro = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"RsGm{tag}", $"rsgm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        Guid gmId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"RSGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
            gmId = user.Id;
        }

        var codigo = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var jogadorRegistro = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"RsJg{tag}", $"rsjg{tag}@teste.com", "Senha!123", "Senha!123", codigo));
        var jogador = (await jogadorRegistro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var jogadorId = (await LerAsync<MeResponse>(jogador, "/api/auth/me")).Id;

        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(jogadorId));
        return new Mesa(gm, gmId, jogador, jogadorId, campaignId, tag);
    }

    /// <summary>Um NPC ou uma Criatura do GM (<paramref name="concedida"/> = false) ou concedida ao jogador da mesa, já com o retrato de partida.</summary>
    private async Task<Ficha> NovaFichaAsync(Mesa mesa, string rota, bool concedida)
    {
        string id;
        if (concedida)
        {
            var grant = await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/grants", mesa.Gm, new GrantSheetRequest(mesa.JogadorId, rota == Npc ? "Npc" : "Creature", null));
            id = (await grant.Content.ReadFromJsonAsync<GrantSheetResponse>())!.SheetId;
        }
        else
        {
            id = (await JsonDocument.ParseAsync(await (await EnviarAsync(HttpMethod.Post, $"/api/{rota}", mesa.Gm)).Content.ReadAsStreamAsync())).RootElement.GetProperty("id").GetString()!;
        }

        foreach (var (atributo, total) in new[] { ("Forca", 10), ("Agilidade", 6), ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });

        var pericias = await JsonAsync(mesa.Gm, $"/api/{rota}/{id}/skills");
        var chave = rota == Criatura
            ? pericias.EnumerateArray().First(p => p.GetProperty("pericia").GetString() is not ("Prontidao" or "Reflexos" or "Fortitude")).GetProperty("pericia").GetString()!
            : "Atletismo";
        await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/skills/{chave}", mesa.Gm, new { Gasto = 1, AtributoEscolhido = "Forca" });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var sheetId = Guid.Parse(id);
        var anel = new Artefato { Id = Guid.NewGuid(), GmId = mesa.GmId, Nome = $"Anel {mesa.Tag}", Peso = 0, Preco = 0, TipoDeAlvo = TipoDeAlvo.Dano, Alvo = "Cortante", Valor = 10 };
        var espada = new Arma { Id = Guid.NewGuid(), GmId = mesa.GmId, Nome = $"Espada {mesa.Tag}", Peso = 0, Preco = 0 };
        db.Set<Item>().AddRange(anel, espada);
        await db.SaveChangesAsync();
        var magiaId = Guid.NewGuid();
        if (rota == Npc)
        {
            db.NpcArtifacts.Add(new NpcArtifact { Id = Guid.NewGuid(), NpcSheetId = sheetId, ArtifactItemId = anel.Id });
            db.NpcWeapons.Add(new NpcWeapon { Id = Guid.NewGuid(), NpcSheetId = sheetId, ItemId = espada.Id, IsEquipped = true });
            db.NpcSpellAbilities.Add(new NpcSpellAbility { Id = magiaId, NpcSheetId = sheetId, Nome = "Bola de Fogo", Tipo = SpellAbilityTipo.Magia, Grau = 2, Descricao = "." });
            var traco = await db.Traits.FirstAsync(t => !t.IsDeleted);
            db.NpcTraits.Add(new NpcTrait { Id = Guid.NewGuid(), NpcSheetId = sheetId, TraitId = traco.Id, Polaridade = traco.Polaridade });
        }
        else
        {
            db.CreatureArtifacts.Add(new CreatureArtifact { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ArtifactItemId = anel.Id });
            db.CreatureWeapons.Add(new CreatureWeapon { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ItemId = espada.Id, IsEquipped = true });
            db.CreatureSpellAbilities.Add(new CreatureSpellAbility { Id = magiaId, CreatureSheetId = sheetId, Nome = "Bola de Fogo", Tipo = SpellAbilityTipo.Magia, Grau = 2, Descricao = "." });
        }
        await db.SaveChangesAsync();
        return new Ficha(rota, id, chave, anel.Id);
    }

    private async Task<string> CriarCondicaoAsync(Mesa mesa, string nome, params Linha[] linhas)
    {
        var request = new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Descrição da condição.", "Veneno", null,
            linhas.Select(l => new CondicaoModificadorDto(l.Alvo, l.Especifico, l.Tipo, l.Valor, null)).ToList(), []);
        return (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;
    }

    private async Task<string> CriarCondicaoQueDesabilitaAsync(Mesa mesa, string nome, params string[] categorias)
    {
        var request = new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Desabilita.", "Veneno", null, [], categorias.ToList());
        return (await (await EnviarAsync(HttpMethod.Post, "/api/condicoes", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoResponse>())!.Id;
    }

    private async Task<string> NovoEncontroAsync(Mesa mesa) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/encounters", mesa.Gm, new CreateEncounterRequest("Encontro"))).Content.ReadFromJsonAsync<EncounterResponse>())!.Id;

    private async Task<string> ParticiparAsync(Mesa mesa, string encounterId, Ficha f)
    {
        var request = new AddParticipantRequest(null, f.Rota == Npc ? f.Id : null, f.Rota == Criatura ? f.Id : null, 10);
        return (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm, request, HttpStatusCode.Created)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;
    }

    private Task IniciarAsync(Mesa mesa, string encounterId) =>
        EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/start", mesa.Gm, esperado: HttpStatusCode.NoContent);

    private async Task<string> AplicarAsync(Mesa mesa, string encounterId, string participantId, string condicaoId, Dictionary<string, List<string>>? itens = null) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, null, null, itens), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;

    private Task<ResumoDeFichaResponse> ResumoAsync(string token, string encounterId, string participantId) =>
        LerAsync<ResumoDeFichaResponse>(token, $"/api/encounters/{encounterId}/participants/{participantId}/resumo");

    /// <summary>Tudo o que os endpoints da ficha devolvem, cru.</summary>
    private async Task<Dictionary<string, string>> RetratoAsync(Ficha f, string token)
    {
        var retrato = new Dictionary<string, string>();
        foreach (var caminho in new[] { "", "/attributes", "/skills", "/sub-attributes", "/modificador-de-dano", "/weapons", "/artifacts", "/spell-abilities", "/condicoes-ativas" })
            retrato[caminho] = await TextoAsync(token, $"/api/{f.Rota}/{f.Id}{caminho}");
        return retrato;
    }

    private static ValorDoResumo Atributo(ResumoDeFichaResponse r, string nome) => r.Atributos.Single(a => a.Nome == nome).Valor;
    private static ValorDoResumo Sub(ResumoDeFichaResponse r, string nome) => r.SubAtributos.Single(a => a.Nome == nome).Valor;
    private static ValorDoResumo Recurso(ResumoDeFichaResponse r, string nome) => r.Recursos.Single(a => a.Nome == nome).Total;

    // ---------- GM-owned: a Condição do participante vale só no resumo ----------

    [Theory]
    [InlineData(Npc)]
    [InlineData(Criatura)]
    public async Task Ficha_do_GM_com_condicao_mostra_os_numeros_alterados_no_resumo_e_a_ficha_nao_muda(string rota)
    {
        var mesa = await MesaAsync($"Gm{rota[..2]}");
        var ficha = await NovaFichaAsync(mesa, rota, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var semCondicao = await ResumoAsync(mesa.Gm, encounterId, participantId);
        semCondicao.Condicoes.Should().BeEmpty();
        Atributo(semCondicao, "Forca").Should().Be(new ValorDoResumo(10, 10));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Fraqueza",
            Fixo("Atributo", "Forca", -3), Fixo("SubAtributo", "Iniciativa", -2), Pct("RecursoTotal", "Vitalidade", -50)));

        var resumo = await ResumoAsync(mesa.Gm, encounterId, participantId);
        resumo.Tipo.Should().Be(rota == Npc ? "Npc" : "Criatura");
        resumo.FichaId.Should().Be(ficha.Id);
        Atributo(resumo, "Forca").Should().Be(new ValorDoResumo(7, 10));
        Atributo(resumo, "Vigor").Should().Be(new ValorDoResumo(8, 8));
        Sub(resumo, "Iniciativa").Valor.Should().Be(Sub(resumo, "Iniciativa").ValorSemCondicoes - 2);
        var vitalidade = Recurso(resumo, "Vitalidade");
        vitalidade.Valor.Should().Be(vitalidade.ValorSemCondicoes / 2);
        var pericia = resumo.Pericias.Single(p => p.Chave == ficha.Pericia);
        pericia.Total.Valor.Should().Be(pericia.Total.ValorSemCondicoes - 3);
        resumo.Condicoes.Should().ContainSingle().Which.Linhas.Should().NotBeEmpty();

        // Review Focus 2: nenhum endpoint da ficha do GM reflete a Condição.
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
    }

    [Fact]
    public async Task Duas_instancias_da_mesma_ficha_mostram_numeros_diferentes()
    {
        var mesa = await MesaAsync("Duas");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var primeira = await ParticiparAsync(mesa, encounterId, ficha);
        var segunda = await ParticiparAsync(mesa, encounterId, ficha);
        var terceira = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);

        await AplicarAsync(mesa, encounterId, primeira, await CriarCondicaoAsync(mesa, "Fraqueza", Fixo("Atributo", "Forca", -3)));
        await AplicarAsync(mesa, encounterId, segunda, await CriarCondicaoAsync(mesa, "Vigor extra", Fixo("Atributo", "Vigor", 4)));

        var a = await ResumoAsync(mesa.Gm, encounterId, primeira);
        var b = await ResumoAsync(mesa.Gm, encounterId, segunda);
        var c = await ResumoAsync(mesa.Gm, encounterId, terceira);
        Atributo(a, "Forca").Should().Be(new ValorDoResumo(7, 10));
        Atributo(a, "Vigor").Should().Be(new ValorDoResumo(8, 8));
        Atributo(b, "Forca").Should().Be(new ValorDoResumo(10, 10));
        Atributo(b, "Vigor").Should().Be(new ValorDoResumo(12, 8));
        Recurso(b, "Vitalidade").Valor.Should().BeGreaterThan(Recurso(a, "Vitalidade").Valor);
        c.Atributos.Should().OnlyContain(x => x.Valor.Valor == x.Valor.ValorSemCondicoes);
        c.Condicoes.Should().BeEmpty();
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
    }

    [Fact]
    public async Task Recursos_de_ficha_do_GM_atuais_sao_os_da_instancia_e_o_total_leva_as_condicoes_dela()
    {
        var mesa = await MesaAsync("Rec");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await EnviarAsync(HttpMethod.Put, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm, new UpdateParticipantRequest(10, 7, 5, 3, 0, []));
        await IniciarAsync(mesa, encounterId);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Vida", Fixo("RecursoTotal", "Vitalidade", 5)));

        var resumo = await ResumoAsync(mesa.Gm, encounterId, participantId);
        var vitalidade = resumo.Recursos.Single(r => r.Nome == "Vitalidade");
        vitalidade.Atual.Should().Be(7);
        vitalidade.Total.Valor.Should().Be(vitalidade.Total.ValorSemCondicoes + 5);
        resumo.Recursos.Single(r => r.Nome == "Foco").Atual.Should().Be(5);
        resumo.Recursos.Single(r => r.Nome == "Adrenalina").Atual.Should().Be(3);
    }

    // ---------- conteúdo ----------

    [Theory]
    [InlineData(Npc)]
    [InlineData(Criatura)]
    public async Task O_resumo_traz_nome_nivel_perícias_com_pontos_equipamento_e_magias_e_marca_o_desabilitado(string rota)
    {
        var mesa = await MesaAsync($"Cont{rota[..2]}");
        var ficha = await NovaFichaAsync(mesa, rota, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);

        var antes = await ResumoAsync(mesa.Gm, encounterId, participantId);
        antes.Nivel.Should().BeGreaterThan(0);
        antes.Atributos.Select(a => a.Nome).Should().Contain(["Forca", "Agilidade", "Vigor", "Astucia"]);
        antes.Pericias.Should().ContainSingle().Which.Chave.Should().Be(ficha.Pericia); // só a que tem pontos
        antes.Equipamentos.Select(e => (e.Categoria, e.DesabilitadoPor)).Should().BeEquivalentTo([("Arma", (string?)null), ("Artefato", (string?)null)]);
        antes.MagiasEHabilidades.Should().ContainSingle().Which.Should().Be(new ResumoDeLinha("Magia", "Bola de Fogo", null));
        if (rota == Npc)
            antes.Caracteristicas.Should().ContainSingle();

        var condicao = await CriarCondicaoQueDesabilitaAsync(mesa, "Antimagia", "Magia", "Artefato");
        await AplicarAsync(mesa, encounterId, participantId, condicao);
        var depois = await ResumoAsync(mesa.Gm, encounterId, participantId);
        var nome = $"Antimagia {mesa.Tag}";
        depois.MagiasEHabilidades.Single().DesabilitadoPor.Should().Be(nome);
        depois.Equipamentos.Single(e => e.Categoria == "Artefato").DesabilitadoPor.Should().Be(nome);
        depois.Equipamentos.Single(e => e.Categoria == "Arma").DesabilitadoPor.Should().BeNull();
        // O Artefato desabilitado deixa de somar: o resumo e só ele.
        depois.Equipamentos.Should().HaveCount(2);
    }

    [Fact]
    public async Task Perícia_sem_pontos_so_aparece_quando_a_condicao_a_altera()
    {
        var mesa = await MesaAsync("Per");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);
        var outra = (await JsonAsync(mesa.Gm, $"/api/npc-sheets/{ficha.Id}/skills")).EnumerateArray()
            .First(p => p.GetProperty("pericia").GetString() != ficha.Pericia).GetProperty("pericia").GetString()!;
        await EnviarAsync(HttpMethod.Put, $"/api/npc-sheets/{ficha.Id}/skills/{outra}", mesa.Gm, new { Gasto = 0, AtributoEscolhido = "Agilidade" });
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Furtivo", Fixo("Pericia", outra, 2)));

        var resumo = await ResumoAsync(mesa.Gm, encounterId, participantId);
        resumo.Pericias.Select(p => p.Chave).Should().BeEquivalentTo(ficha.Pericia, outra);
        var furtividade = resumo.Pericias.Single(p => p.Chave == outra);
        furtividade.Gasto.Should().Be(0);
        furtividade.Total.Valor.Should().Be(furtividade.Total.ValorSemCondicoes + 2);
    }

    // ---------- concedida: igual à própria ficha ----------

    [Theory]
    [InlineData(Npc)]
    [InlineData(Criatura)]
    public async Task Ficha_concedida_mostra_os_mesmos_numeros_que_os_endpoints_da_propria_ficha(string rota)
    {
        var mesa = await MesaAsync($"Cnc{rota[..2]}");
        var ficha = await NovaFichaAsync(mesa, rota, concedida: true);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Fraqueza",
            Fixo("Atributo", "Forca", -3), Pct("Atributo", "Vigor", -50), Fixo("SubAtributo", "Iniciativa", -2)));

        var resumo = await ResumoAsync(mesa.Gm, encounterId, participantId);
        var raiz = $"/api/{ficha.Rota}/{ficha.Id}";
        foreach (var atributo in (await JsonAsync(mesa.Gm, $"{raiz}/attributes")).EnumerateArray())
            Atributo(resumo, atributo.GetProperty("atributo").GetString()!).Valor.Should().Be(atributo.GetProperty("total").GetInt32());
        var pericia = (await JsonAsync(mesa.Gm, $"{raiz}/skills")).EnumerateArray().Single(p => p.GetProperty("pericia").GetString() == ficha.Pericia);
        resumo.Pericias.Single(p => p.Chave == ficha.Pericia).Total.Valor.Should().Be(pericia.GetProperty("total").GetInt32());
        var sub = await JsonAsync(mesa.Gm, $"{raiz}/sub-attributes");
        Sub(resumo, "Iniciativa").Valor.Should().Be(sub.GetProperty("iniciativa").GetInt32());
        Sub(resumo, "Defesa Natural").Valor.Should().Be(sub.GetProperty("defesaNatural").GetInt32());
        var fichaJson = await JsonAsync(mesa.Gm, raiz);
        Recurso(resumo, "Vitalidade").Valor.Should().Be(fichaJson.GetProperty("vitalidadeMaximo").GetInt32());
        resumo.Recursos.Single(r => r.Nome == "Vitalidade").Atual.Should().Be(fichaJson.GetProperty("vitalidadeAtual").GetInt32());
        // O dono vê os mesmos números na própria ficha.
        (await TextoAsync(mesa.Jogador, $"{raiz}/attributes")).Should().Be(await TextoAsync(mesa.Gm, $"{raiz}/attributes"));
        Atributo(resumo, "Forca").Should().Be(new ValorDoResumo(7, 10));
    }

    [Fact]
    public async Task Ficha_concedida_leva_tambem_as_condicoes_de_outro_encontro_em_andamento_como_a_propria_ficha()
    {
        var mesa = await MesaAsync("Outro");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: true);
        var primeiro = await NovoEncontroAsync(mesa);
        var segundo = await NovoEncontroAsync(mesa);
        var participanteUm = await ParticiparAsync(mesa, primeiro, ficha);
        var participanteDois = await ParticiparAsync(mesa, segundo, ficha);
        await IniciarAsync(mesa, primeiro);
        await IniciarAsync(mesa, segundo);
        await AplicarAsync(mesa, primeiro, participanteUm, await CriarCondicaoAsync(mesa, "Fraqueza", Fixo("Atributo", "Forca", -3)));
        await AplicarAsync(mesa, segundo, participanteDois, await CriarCondicaoAsync(mesa, "Lentidão", Fixo("Atributo", "Forca", -2)));

        var resumo = await ResumoAsync(mesa.Gm, primeiro, participanteUm);
        var propria = await JsonAsync(mesa.Gm, $"/api/npc-sheets/{ficha.Id}/attributes");
        Atributo(resumo, "Forca").Valor.Should().Be(propria.EnumerateArray().Single(a => a.GetProperty("atributo").GetString() == "Forca").GetProperty("total").GetInt32());
        Atributo(resumo, "Forca").Valor.Should().Be(5);
        resumo.Condicoes.Should().HaveCount(2);
    }

    // ---------- encontro finalizado, erros e acesso ----------

    [Fact]
    public async Task Encontro_finalizado_responde_sem_condicoes_ativas()
    {
        var mesa = await MesaAsync("Fim");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await IniciarAsync(mesa, encounterId);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Fraqueza", Fixo("Atributo", "Forca", -3)));
        await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/finish", mesa.Gm, esperado: HttpStatusCode.NoContent);

        var resumo = await ResumoAsync(mesa.Gm, encounterId, participantId);
        Atributo(resumo, "Forca").Should().Be(new ValorDoResumo(10, 10));
        resumo.Condicoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Participante_de_Personagem_responde_400()
    {
        var mesa = await MesaAsync("Pers");
        var personagem = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm,
            new RuinaRPG.Contracts.CharacterSheets.CreateCharacterSheetRequest(mesa.JogadorId))).Content.ReadFromJsonAsync<RuinaRPG.Contracts.CharacterSheets.CharacterSheetResponse>())!.Id;
        var encounterId = await NovoEncontroAsync(mesa);
        var participante = (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants", mesa.Gm,
            new AddParticipantRequest(personagem, null, null, 10), HttpStatusCode.Created)).Content.ReadFromJsonAsync<EncounterParticipantResponse>())!.Id;

        var response = await EnviarAsync(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{participante}/resumo", mesa.Gm, esperado: HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("O resumo é só para NPCs e Criaturas.");
    }

    [Fact]
    public async Task Outro_GM_e_um_jogador_recebem_404()
    {
        var mesa = await MesaAsync("Acesso");
        var outro = await MesaAsync("Acesso2");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        var url = $"/api/encounters/{encounterId}/participants/{participantId}/resumo";

        await EnviarAsync(HttpMethod.Get, url, outro.Gm, esperado: HttpStatusCode.NotFound);
        await EnviarAsync(HttpMethod.Get, url, mesa.Jogador, esperado: HttpStatusCode.Forbidden);
        await EnviarAsync(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{Guid.NewGuid()}/resumo", mesa.Gm, esperado: HttpStatusCode.NotFound);
        (await _client.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Ficha_de_origem_apagada_responde_404()
    {
        var mesa = await MesaAsync("Apag");
        var ficha = await NovaFichaAsync(mesa, Npc, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await EnviarAsync(HttpMethod.Delete, $"/api/npc-sheets/{ficha.Id}", mesa.Gm, esperado: HttpStatusCode.NoContent);

        await EnviarAsync(HttpMethod.Get, $"/api/encounters/{encounterId}/participants/{participantId}/resumo", mesa.Gm, esperado: HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_lista_de_participantes_diz_a_origem_de_cada_um()
    {
        var mesa = await MesaAsync("Orig");
        var npc = await NovaFichaAsync(mesa, Npc, concedida: false);
        var criatura = await NovaFichaAsync(mesa, Criatura, concedida: false);
        var encounterId = await NovoEncontroAsync(mesa);
        await ParticiparAsync(mesa, encounterId, npc);
        await ParticiparAsync(mesa, encounterId, criatura);

        var lista = await LerAsync<List<EncounterParticipantResponse>>(mesa.Gm, $"/api/encounters/{encounterId}/participants");
        lista.Select(p => p.Origem).Should().BeEquivalentTo(["Npc", "Criatura"]);
    }
}
