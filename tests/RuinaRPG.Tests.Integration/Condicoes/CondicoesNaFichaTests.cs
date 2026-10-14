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
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.CreatureSheets;
using RuinaRPG.Infrastructure.Items;
using RuinaRPG.Infrastructure.NpcSheets;
using RuinaRPG.Infrastructure.Persistence;

namespace RuinaRPG.Tests.Integration.Condicoes;

/// <summary>
/// As Condições ativas mudam os números CALCULADOS da ficha enquanto estão ativas, e só então. Toda ficha destes
/// testes parte do mesmo retrato: Força 10, Agilidade 6, Vigor 8, Astúcia 7; uma Perícia sobre Força (total 10);
/// a Maestria "Espadas" (3 + 0 + Força = 13); Artefatos de Dano Cortante +10 e Redução Física +4; e, onde a ficha
/// tem Afinidades, a Afinidade Fogo com uma linha de Fogo valendo 6 (Eficiência 3 e Dano 3 na Tabela de Afinidades).
/// </summary>
public class CondicoesNaFichaTests : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string Personagem = "character-sheets";
    private const string Npc = "npc-sheets";
    private const string Criatura = "creature-sheets";

    private readonly PostgresFixture _postgres;
    private ApiFactory _factory = null!;
    private HttpClient _client = null!;

    public CondicoesNaFichaTests(PostgresFixture postgres) => _postgres = postgres;

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

    private sealed record Mesa(string Gm, Guid GmId, string Jogador, string JogadorId, string CampaignId, string Tag);

    /// <summary>Uma ficha de qualquer tipo; <c>Pericia</c> é a chave da Perícia posta sobre Força, <c>OutraPericia</c> a de outra qualquer.</summary>
    private sealed record Ficha(string Rota, string Id, string Pericia, string NomeDaPericia, string OutraPericia)
    {
        public bool TemAfinidades => Rota != Criatura;
    }

    private sealed record Linha(string Alvo, string? Especifico, string Tipo, int Valor);

    private static Linha Fixo(string alvo, string? especifico, int valor) => new(alvo, especifico, "Fixo", valor);
    private static Linha Pct(string alvo, string? especifico, int valor) => new(alvo, especifico, "Percentual", valor);

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

    private async Task<string> TextoAsync(string token, string url) =>
        await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadAsStringAsync();

    private async Task<JsonElement> JsonAsync(string token, string url) =>
        JsonDocument.Parse(await TextoAsync(token, url)).RootElement;

    private async Task<T> LerAsync<T>(string token, string url) =>
        (await (await EnviarAsync(HttpMethod.Get, url, token, esperado: HttpStatusCode.OK)).Content.ReadFromJsonAsync<T>())!;

    private async Task<Mesa> MesaAsync(string tag)
    {
        var registro = await _client.PostAsJsonAsync("/api/auth/register/gm", new RegisterGmRequest($"CfGm{tag}", $"cfgm{tag}@teste.com", "Senha!123", "Senha!123"));
        var gm = (await registro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        Guid gmId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
            var user = await db.Users.SingleAsync(u => u.NormalizedEmail == $"CFGM{tag}@TESTE.COM".ToUpperInvariant());
            user.IsRulesAuditor = true; // o mesmo usuário mantém o catálogo de Condições
            await db.SaveChangesAsync();
            gmId = user.Id;
        }

        var codigo = (await (await EnviarAsync(HttpMethod.Post, "/api/invite-codes", gm)).Content.ReadFromJsonAsync<RuinaRPG.Contracts.Invites.InviteCodeResponse>())!.Code;
        var jogadorRegistro = await _client.PostAsJsonAsync("/api/auth/register/jogador", new RegisterJogadorRequest($"CfJg{tag}", $"cfjg{tag}@teste.com", "Senha!123", "Senha!123", codigo));
        var jogador = (await jogadorRegistro.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        var jogadorId = (await LerAsync<MeResponse>(jogador, "/api/auth/me")).Id;

        var campaignId = (await (await EnviarAsync(HttpMethod.Post, "/api/campaigns", gm, new CreateCampaignRequest($"Campanha {tag}", ""))).Content.ReadFromJsonAsync<CampaignResponse>())!.Id;
        await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campaignId}/members", gm, new AddCampaignMemberRequest(jogadorId));
        return new Mesa(gm, gmId, jogador, jogadorId, campaignId, tag);
    }

    private async Task<Ficha> NovoPersonagemAsync(Mesa mesa)
    {
        var id = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/character-sheets", mesa.Gm, new CreateCharacterSheetRequest(mesa.JogadorId)))
            .Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        return await PrepararAsync(mesa, Personagem, id);
    }

    /// <summary>Um NPC ou uma Criatura do GM (<paramref name="concedida"/> = false) ou concedida ao jogador da mesa.</summary>
    private async Task<Ficha> NovaFichaDoGmAsync(Mesa mesa, string rota, bool concedida)
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
        return await PrepararAsync(mesa, rota, id);
    }

    private async Task<Ficha> PrepararAsync(Mesa mesa, string rota, string id)
    {
        foreach (var (atributo, total) in new[] { ("Forca", 10), ("Agilidade", 6), ("Vigor", 8), ("Astucia", 7) })
            await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/attributes/{atributo}", mesa.Gm, new { Gasto = 0, Bonus = total, TemMaestria = true });

        var pericias = await JsonAsync(mesa.Gm, $"/api/{rota}/{id}/skills");
        var escolhida = rota == Criatura
            ? pericias.EnumerateArray().First(p => p.GetProperty("pericia").GetString() is not ("Prontidao" or "Reflexos" or "Fortitude"))
            : pericias.EnumerateArray().Single(p => p.GetProperty("pericia").GetString() == "Atletismo");
        var chave = escolhida.GetProperty("pericia").GetString()!;
        await EnviarAsync(HttpMethod.Put, $"/api/{rota}/{id}/skills/{chave}", mesa.Gm, new { Gasto = 0, AtributoEscolhido = "Forca" });
        await EnviarAsync(HttpMethod.Post, $"/api/{rota}/{id}/masteries", mesa.Gm, new { Nome = "Espadas", Pericia = chave, Atributo = "Forca", GastoMaestria = 3 });

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var sheetId = Guid.Parse(id);
        var cortante = new Artefato { Id = Guid.NewGuid(), GmId = mesa.GmId, Nome = "Gume", Peso = 0, Preco = 0, TipoDeAlvo = TipoDeAlvo.Dano, Alvo = "Cortante", Valor = 10 };
        var reducao = new Artefato { Id = Guid.NewGuid(), GmId = mesa.GmId, Nome = "Placa", Peso = 0, Preco = 0, TipoDeAlvo = TipoDeAlvo.SubAtributo, Alvo = SubAtributoAlvo.ReducaoFisica, Valor = 4 };
        db.Set<Artefato>().AddRange(cortante, reducao);
        foreach (var artefato in new[] { cortante, reducao })
        {
            if (rota == Personagem)
                db.CharacterArtifacts.Add(new CharacterArtifact { Id = Guid.NewGuid(), CharacterSheetId = sheetId, ArtifactItemId = artefato.Id });
            else if (rota == Npc)
                db.NpcArtifacts.Add(new NpcArtifact { Id = Guid.NewGuid(), NpcSheetId = sheetId, ArtifactItemId = artefato.Id });
            else
                db.CreatureArtifacts.Add(new CreatureArtifact { Id = Guid.NewGuid(), CreatureSheetId = sheetId, ArtifactItemId = artefato.Id });
        }
        if (rota == Personagem)
        {
            (await db.CharacterSheets.FindAsync(sheetId))!.Afinidade = AfinidadeElemental.Fogo;
            db.CharacterAffinities.Add(new CharacterAffinity { Id = Guid.NewGuid(), CharacterSheetId = sheetId, Elemento = Elemento.Fogo, ElementoValor = 6 });
        }
        else if (rota == Npc)
        {
            (await db.NpcSheets.FindAsync(sheetId))!.Afinidade = AfinidadeElemental.Fogo;
            db.NpcAffinities.Add(new NpcAffinity { Id = Guid.NewGuid(), NpcSheetId = sheetId, Elemento = Elemento.Fogo, ElementoValor = 6 });
        }
        await db.SaveChangesAsync();

        var outra = pericias.EnumerateArray().Select(p => p.GetProperty("pericia").GetString()!).First(c => c != chave);
        return new Ficha(rota, id, chave, escolhida.GetProperty("nome").GetString()!, outra);
    }

    private static readonly string[] SubAtributos = ["iniciativa", "movimentacao", "esquivaNatural", "defesaNatural", "reducaoFisica", "reducaoMagica", "eficienciaElemental", "danoElemental"];
    private static readonly string[] Recursos = ["vitalidade", "foco", "adrenalina", "estresse"];

    /// <summary>Os números que estes testes acompanham, lidos dos endpoints que as telas da ficha usam.</summary>
    private async Task<SortedDictionary<string, int>> NumerosAsync(Ficha f, string token)
    {
        var n = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var raiz = $"/api/{f.Rota}/{f.Id}";

        var ficha = await JsonAsync(token, raiz);
        foreach (var recurso in Recursos)
        {
            if (ficha.TryGetProperty($"{recurso}Maximo", out var maximo))
                n[$"Maximo:{recurso}"] = maximo.GetInt32();
            if (ficha.TryGetProperty($"{recurso}Atual", out var atual))
                n[$"Atual:{recurso}"] = atual.GetInt32();
        }

        foreach (var a in (await JsonAsync(token, $"{raiz}/attributes")).EnumerateArray())
            n[$"Atributo:{a.GetProperty("atributo").GetString()}"] = a.GetProperty("total").GetInt32();

        var pericia = (await JsonAsync(token, $"{raiz}/skills")).EnumerateArray().Single(p => p.GetProperty("pericia").GetString() == f.Pericia);
        n["Pericia"] = pericia.GetProperty("total").GetInt32();

        var sub = await JsonAsync(token, $"{raiz}/sub-attributes");
        foreach (var nome in SubAtributos)
        {
            if (sub.TryGetProperty(nome, out var valor) && valor.ValueKind == JsonValueKind.Number)
                n[$"Sub:{nome}"] = valor.GetInt32();
        }

        var dano = await JsonAsync(token, $"{raiz}/modificador-de-dano");
        foreach (var tipo in new[] { "cortante", "perfurante", "contundente", "arcano" })
            n[$"Dano:{tipo}"] = dano.GetProperty(tipo).GetInt32();
        foreach (var elemento in dano.GetProperty("danoElemental").EnumerateObject())
            n[$"DanoElemental:{elemento.Name}"] = elemento.Value.GetInt32();

        foreach (var m in (await JsonAsync(token, $"{raiz}/masteries")).EnumerateArray())
            n[$"Maestria:{m.GetProperty("nome").GetString()}"] = m.GetProperty("total").GetInt32();

        if (f.TemAfinidades)
        {
            foreach (var a in (await JsonAsync(token, $"{raiz}/affinities")).EnumerateArray())
            {
                var elemento = a.GetProperty("elemento").GetString();
                n[$"Afinidade:{elemento}"] = a.GetProperty("elementoValorComCondicoes").GetInt32();
                n[$"Afinidade:{elemento}.guardada"] = a.GetProperty("elementoValor").GetInt32();
            }
        }
        return n;
    }

    /// <summary>Tudo o que os endpoints da ficha devolvem, cru: valores guardados e calculados, orçamentos e penalidades.</summary>
    private async Task<Dictionary<string, string>> RetratoAsync(Ficha f, string token)
    {
        var caminhos = new List<string> { "", "/attributes", "/attributes/budget", "/skills", "/sub-attributes", "/modificador-de-dano", "/masteries", "/equipment-penalties", "/spell-abilities" };
        if (f.Rota == Personagem)
            caminhos.Add("/skills/budget");
        if (f.TemAfinidades)
            caminhos.Add("/affinities");

        var retrato = new Dictionary<string, string>();
        foreach (var caminho in caminhos)
            retrato[caminho] = await TextoAsync(token, $"/api/{f.Rota}/{f.Id}{caminho}");
        return retrato;
    }

    private static SortedDictionary<string, int> Com(SortedDictionary<string, int> antes, params (string Chave, int Valor)[] mudancas)
    {
        var copia = new SortedDictionary<string, int>(antes, StringComparer.Ordinal);
        foreach (var (chave, valor) in mudancas)
        {
            copia.Should().ContainKey(chave, "a mudança esperada tem de ser de um número que a ficha já mostra");
            copia[chave] = valor;
        }
        return copia;
    }

    private async Task<string> CriarCondicaoAsync(Mesa mesa, string nome, int? duracao, params Linha[] linhas)
    {
        var request = new SalvarCondicaoRequest($"{nome} {mesa.Tag}", "Descrição da condição.", "Veneno", duracao,
            linhas.Select(l => new CondicaoModificadorDto(l.Alvo, l.Especifico, l.Tipo, l.Valor, null)).ToList(), []);
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

    private async Task<string> AplicarAsync(Mesa mesa, string encounterId, string participantId, string condicaoId, int? duracao = null) =>
        (await (await EnviarAsync(HttpMethod.Post, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes", mesa.Gm,
            new AplicarCondicaoRequest(condicaoId, duracao, null, null), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CondicaoAplicadaResponse>())!.Id;

    private Task RemoverAsync(Mesa mesa, string encounterId, string participantId, string aplicadaId) =>
        EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}/condicoes/{aplicadaId}", mesa.Gm, esperado: HttpStatusCode.NoContent);

    /// <summary>Um encontro em andamento com a ficha como único participante (cada "advance-turn" é um Turno novo).</summary>
    private async Task<(string EncounterId, string ParticipantId)> EncontroEmAndamentoAsync(Mesa mesa, Ficha f)
    {
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, f);
        await EncontroAsync(mesa, encounterId, "start");
        return (encounterId, participantId);
    }

    /// <summary>
    /// Para cada caso: aplica uma Condição só com aquela linha, confere TODOS os números acompanhados (os que mudam
    /// e os que não podem mudar) e, depois de remover, que tudo voltou exatamente ao início.
    /// </summary>
    private async Task ConferirLinhasAsync(Mesa mesa, Ficha f, params (Linha Linha, (string Chave, int Antes, int Durante)[] Mudancas)[] casos)
    {
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, f);
        var numerosAntes = await NumerosAsync(f, mesa.Gm);
        var retratoAntes = await RetratoAsync(f, mesa.Gm);

        var indice = 0;
        foreach (var (linha, mudancas) in casos)
        {
            var caso = $"{linha.Alvo}/{linha.Especifico ?? "(geral)"} {linha.Tipo} {linha.Valor}";
            foreach (var (chave, antes, _) in mudancas)
                numerosAntes.Should().Contain(chave, antes, $"valor de partida de {caso}");

            var condicaoId = await CriarCondicaoAsync(mesa, $"Caso {indice++}", null, linha);
            var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, condicaoId);
            (await NumerosAsync(f, mesa.Gm)).Should().Equal(Com(numerosAntes, mudancas.Select(m => (m.Chave, m.Durante)).ToArray()), $"durante {caso}");

            await RemoverAsync(mesa, encounterId, participantId, aplicadaId);
            (await RetratoAsync(f, mesa.Gm)).Should().Equal(retratoAntes, $"depois de remover {caso}");
        }
    }

    private static (Linha, (string, int, int)[]) Caso(Linha linha, params (string Chave, int Antes, int Durante)[] mudancas) => (linha, mudancas);

    // ---------- o retrato de partida ----------

    [Fact]
    public async Task Sem_condicao_a_ficha_mostra_o_retrato_de_partida_e_dano_elemental_zero()
    {
        var mesa = await MesaAsync("Base");
        var ficha = await NovoPersonagemAsync(mesa);

        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(new SortedDictionary<string, int>(StringComparer.Ordinal)
        {
            ["Afinidade:Fogo"] = 6, ["Afinidade:Fogo.guardada"] = 6,
            ["Atributo:Agilidade"] = 6, ["Atributo:Astucia"] = 7, ["Atributo:Destreza"] = 0, ["Atributo:Forca"] = 10,
            ["Atributo:Influencia"] = 0, ["Atributo:Instinto"] = 0, ["Atributo:Vigor"] = 8, ["Atributo:Vontade"] = 0,
            ["Atual:adrenalina"] = 0, ["Atual:estresse"] = 0, ["Atual:foco"] = 0, ["Atual:vitalidade"] = 0,
            ["Dano:arcano"] = 0, ["Dano:contundente"] = 0, ["Dano:cortante"] = 10, ["Dano:perfurante"] = 0,
            ["DanoElemental:Agua"] = 0, ["DanoElemental:Ar"] = 0, ["DanoElemental:Fogo"] = 0, ["DanoElemental:Terra"] = 0,
            ["Maestria:Espadas"] = 13,
            ["Maximo:adrenalina"] = 10, ["Maximo:estresse"] = 10, ["Maximo:foco"] = 14, ["Maximo:vitalidade"] = 16,
            ["Pericia"] = 10,
            ["Sub:danoElemental"] = 3, ["Sub:defesaNatural"] = 8, ["Sub:eficienciaElemental"] = 3, ["Sub:esquivaNatural"] = 6,
            ["Sub:iniciativa"] = 6, ["Sub:movimentacao"] = 12, ["Sub:reducaoFisica"] = 4, ["Sub:reducaoMagica"] = 0,
        });
    }

    // ---------- cada alvo, com linha fixa e com linha percentual ----------

    [Fact]
    public async Task Atributo_muda_o_total_e_cada_dependente_uma_unica_vez()
    {
        var mesa = await MesaAsync("Atr");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("Atributo", "Forca", -3), ("Atributo:Forca", 10, 7), ("Pericia", 10, 7), ("Maestria:Espadas", 13, 10)),
            // −50% de Força: a Perícia e a Maestria caem 5 (o que a Força caiu), não são cortadas de novo pela metade.
            Caso(Pct("Atributo", "Forca", -50), ("Atributo:Forca", 10, 5), ("Pericia", 10, 5), ("Maestria:Espadas", 13, 8)),
            Caso(Pct("Atributo", "Agilidade", -50), ("Atributo:Agilidade", 6, 3), ("Sub:iniciativa", 6, 3), ("Sub:movimentacao", 12, 6), ("Sub:esquivaNatural", 6, 3)),
            Caso(Fixo("Atributo", "Agilidade", 2), ("Atributo:Agilidade", 6, 8), ("Sub:iniciativa", 6, 8), ("Sub:movimentacao", 12, 16), ("Sub:esquivaNatural", 6, 8)),
            Caso(Pct("Atributo", "Vigor", -50), ("Atributo:Vigor", 8, 4), ("Sub:defesaNatural", 8, 4), ("Maximo:vitalidade", 16, 8)),
            Caso(Fixo("Atributo", "Astucia", -1), ("Atributo:Astucia", 7, 6), ("Maximo:foco", 14, 12)),
            // Um atributo pode ficar negativo sem Condições (penalidade de equipamento), então a Condição também pode.
            Caso(Fixo("Atributo", "Destreza", -2), ("Atributo:Destreza", 0, -2)));
    }

    [Fact]
    public async Task Atributo_reduzido_muda_o_peso_maximo_calculado_sobre_o_atributo_ajustado()
    {
        var mesa = await MesaAsync("Peso");
        var ficha = await NovoPersonagemAsync(mesa);
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        var antes = await LerAsync<SubAttributesResponse>(mesa.Gm, $"/api/{ficha.Rota}/{ficha.Id}/sub-attributes");
        antes.PesoMaximo.Should().Be(CarryWeightCalculator.PesoMaximo(10, 8, 0m, 0m));

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Fraqueza", null, Pct("Atributo", "Forca", -50)));

        var durante = await LerAsync<SubAttributesResponse>(mesa.Gm, $"/api/{ficha.Rota}/{ficha.Id}/sub-attributes");
        durante.PesoMaximo.Should().Be(CarryWeightCalculator.PesoMaximo(5, 8, 0m, 0m));
        durante.PesoMaximo.Should().NotBe(antes.PesoMaximo);
    }

    [Fact]
    public async Task Pericia_muda_so_o_total_da_pericia()
    {
        var mesa = await MesaAsync("Per");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("Pericia", ficha.Pericia, 2), ("Pericia", 10, 12)),
            Caso(Pct("Pericia", null, -25), ("Pericia", 10, 7)), // piso de 7,5
            Caso(Fixo("Pericia", ficha.OutraPericia, 4))); // outra Perícia: a acompanhada não muda
    }

    [Fact]
    public async Task SubAtributo_muda_cada_sub_atributo_depois_dos_artefatos()
    {
        var mesa = await MesaAsync("Sub");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("SubAtributo", "Esquiva Natural", -2), ("Sub:esquivaNatural", 6, 4)),
            Caso(Pct("SubAtributo", "Defesa Natural", 50), ("Sub:defesaNatural", 8, 12)),
            Caso(Fixo("SubAtributo", "Iniciativa", -10), ("Sub:iniciativa", 6, -4)), // sem piso, como sem Condições
            Caso(Fixo("SubAtributo", "Movimentação", -3), ("Sub:movimentacao", 12, 9)),
            Caso(Pct("SubAtributo", "Movimentação", -100), ("Sub:movimentacao", 12, 1)), // a fórmula nunca dá menos de 1
            Caso(Pct("SubAtributo", "Redução Física", -50), ("Sub:reducaoFisica", 4, 2)), // sobre o Artefato (+4)
            Caso(Fixo("SubAtributo", "Redução Mágica", 3), ("Sub:reducaoMagica", 0, 3)),
            // Adrenalina é o "sub-atributo" que os Artefatos somam ao total de Adrenalina.
            Caso(Fixo("SubAtributo", "Adrenalina", 2), ("Maximo:adrenalina", 10, 12)));
    }

    [Fact]
    public async Task Dano_muda_os_modificadores_de_dano_depois_dos_artefatos()
    {
        var mesa = await MesaAsync("Dano");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("Dano", "Cortante", 3), ("Dano:cortante", 10, 13)),
            Caso(Pct("Dano", null, -50), ("Dano:cortante", 10, 5)), // os outros três valem 0: metade de 0 é 0
            Caso(Fixo("Dano", null, 2), ("Dano:cortante", 10, 12), ("Dano:perfurante", 0, 2), ("Dano:contundente", 0, 2), ("Dano:arcano", 0, 2)),
            Caso(Fixo("Dano", "Arcano", -4), ("Dano:arcano", 0, -4)));
    }

    [Fact]
    public async Task RecursoTotal_muda_os_maximos_com_piso_zero()
    {
        var mesa = await MesaAsync("Rec");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("RecursoTotal", "Vitalidade", -6), ("Maximo:vitalidade", 16, 10)),
            Caso(Pct("RecursoTotal", "Vitalidade", 25), ("Maximo:vitalidade", 16, 20)),
            Caso(Pct("RecursoTotal", "Foco", 50), ("Maximo:foco", 14, 21)),
            Caso(Fixo("RecursoTotal", "Foco", 3), ("Maximo:foco", 14, 17)),
            Caso(Pct("RecursoTotal", "Adrenalina", -30), ("Maximo:adrenalina", 10, 7)),
            Caso(Fixo("RecursoTotal", "Adrenalina", 5), ("Maximo:adrenalina", 10, 15)),
            Caso(Pct("RecursoTotal", "Estresse", -15), ("Maximo:estresse", 10, 8)), // piso de 8,5
            Caso(Fixo("RecursoTotal", "Estresse", -15), ("Maximo:estresse", 10, 0))); // nunca negativo
    }

    [Fact]
    public async Task AfinidadeElemental_muda_o_valor_exibido_e_o_que_depende_dele_sem_tocar_no_valor_guardado()
    {
        var mesa = await MesaAsync("Afi");
        var ficha = await NovoPersonagemAsync(mesa);
        var arcanaAntes = await LerAsync<CharacterSheetResponse>(mesa.Gm, $"/api/character-sheets/{ficha.Id}");

        await ConferirLinhasAsync(mesa, ficha,
            // 6 → 8 na Tabela de Afinidades: Eficiência 3 → 4, Dano 3 → 4.
            Caso(Fixo("AfinidadeElemental", "Fogo", 2), ("Afinidade:Fogo", 6, 8), ("Sub:eficienciaElemental", 3, 4), ("Sub:danoElemental", 3, 4)),
            // 6 → 3: Eficiência 2, Dano 1.
            Caso(Pct("AfinidadeElemental", null, -50), ("Afinidade:Fogo", 6, 3), ("Sub:eficienciaElemental", 3, 2), ("Sub:danoElemental", 3, 1)),
            Caso(Fixo("AfinidadeElemental", null, -9), ("Afinidade:Fogo", 6, 0), ("Sub:eficienciaElemental", 3, 0), ("Sub:danoElemental", 3, 0)), // nunca negativa
            Caso(Fixo("AfinidadeElemental", "Agua", 5))); // outro Elemento: nada muda

        // O contador de Vocação Arcana usa sempre os valores guardados.
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Afinado", null, Fixo("AfinidadeElemental", null, 7)));
        var arcanaDurante = await LerAsync<CharacterSheetResponse>(mesa.Gm, $"/api/character-sheets/{ficha.Id}");
        arcanaDurante.VocacaoArcanaGasta.Should().Be(arcanaAntes.VocacaoArcanaGasta);
        arcanaDurante.VocacaoArcanaMaxima.Should().Be(arcanaAntes.VocacaoArcanaMaxima);
    }

    [Fact]
    public async Task DanoElemental_e_um_modificador_por_elemento_que_so_existe_com_condicao()
    {
        var mesa = await MesaAsync("DanoElem");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("DanoElemental", "Fogo", 4), ("DanoElemental:Fogo", 0, 4)),
            Caso(Fixo("DanoElemental", null, -2), ("DanoElemental:Ar", 0, -2), ("DanoElemental:Agua", 0, -2), ("DanoElemental:Fogo", 0, -2), ("DanoElemental:Terra", 0, -2)),
            // O modificador parte de 0, então um percentual sozinho não muda nada (percentual de 0).
            Caso(Pct("DanoElemental", "Fogo", 50)));

        // Com um fixo junto, o percentual incide sobre ele: (0 + 4) × 1,5 = 6.
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Em chamas", null, Fixo("DanoElemental", "Fogo", 4), Pct("DanoElemental", null, 50)));
        (await NumerosAsync(ficha, mesa.Gm))["DanoElemental:Fogo"].Should().Be(6);
    }

    [Fact]
    public async Task Maestria_muda_o_total_da_maestria()
    {
        var mesa = await MesaAsync("Mae");
        var ficha = await NovoPersonagemAsync(mesa);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Fixo("Maestria", ficha.Pericia, 2), ("Maestria:Espadas", 13, 15)),
            Caso(Pct("Maestria", null, -50), ("Maestria:Espadas", 13, 6)), // piso de 6,5
            Caso(Fixo("Maestria", ficha.OutraPericia, 9))); // Maestria de outra Perícia
    }

    [Fact]
    public async Task Fixo_e_percentual_juntos_e_duas_condicoes_no_mesmo_participante()
    {
        var mesa = await MesaAsync("Mix");
        var ficha = await NovoPersonagemAsync(mesa);
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        var antes = await NumerosAsync(ficha, mesa.Gm);

        // (10 − 2) × 0,5 = 4.
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Exausto", null, Fixo("Atributo", "Forca", -2), Pct("Atributo", "Forca", -50)));
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(antes, ("Atributo:Forca", 4), ("Pericia", 4), ("Maestria:Espadas", 7)));

        // Mais −60% de outra Condição: −110% ao todo zera, não inverte o sinal.
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Paralisado", null, Pct("Atributo", "Forca", -60)));
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(antes, ("Atributo:Forca", 0), ("Pericia", 0), ("Maestria:Espadas", 3)));
    }

    // ---------- quando a Condição vale ----------

    private static readonly Linha[] LinhasDeTodosOsAlvos =
    [
        Fixo("Atributo", "Forca", -4), Pct("Atributo", "Agilidade", -50), Fixo("Pericia", null, 1), Pct("SubAtributo", "Defesa Natural", 50),
        Fixo("Dano", "Cortante", -3), Fixo("RecursoTotal", "Vitalidade", 4), Pct("RecursoTotal", "Estresse", -50),
        Fixo("AfinidadeElemental", "Fogo", 2), Fixo("DanoElemental", "Fogo", 3), Fixo("Maestria", null, 2),
    ];

    private static (string, int)[] MudancasDeTodosOsAlvos =>
    [
        ("Atributo:Forca", 6), ("Atributo:Agilidade", 3), ("Pericia", 7), ("Maestria:Espadas", 11),
        ("Sub:iniciativa", 3), ("Sub:movimentacao", 6), ("Sub:esquivaNatural", 3), ("Sub:defesaNatural", 12),
        ("Dano:cortante", 7), ("Maximo:vitalidade", 20), ("Maximo:estresse", 5),
        ("Afinidade:Fogo", 8), ("Sub:eficienciaElemental", 4), ("Sub:danoElemental", 4), ("DanoElemental:Fogo", 3),
    ];

    [Fact]
    public async Task So_vale_com_o_encontro_em_andamento_preparacao_finalizar_reabrir_e_remover()
    {
        var mesa = await MesaAsync("Vida");
        var ficha = await NovoPersonagemAsync(mesa);
        var numerosAntes = await NumerosAsync(ficha, mesa.Gm);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var durante = Com(numerosAntes, MudancasDeTodosOsAlvos);

        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Tudo", null, LinhasDeTodosOsAlvos));

        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes, "na preparação (Turno 0) a Condição ainda não vale");

        await EncontroAsync(mesa, encounterId, "start");
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(durante, "iniciado o encontro, a Condição aplicada na preparação passa a valer");
        (await NumerosAsync(ficha, mesa.Jogador)).Should().Equal(durante, "o jogador dono vê os mesmos números");

        await EncontroAsync(mesa, encounterId, "finish");
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes, "encontro finalizado não tem Condição ativa");

        await EncontroAsync(mesa, encounterId, "reopen");
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(durante, "reabrir reativa o que continua no participante");

        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes, "removida a Condição");
    }

    [Fact]
    public async Task Expira_sozinha_quando_o_prazo_em_turnos_vence()
    {
        var mesa = await MesaAsync("Exp");
        var ficha = await NovoPersonagemAsync(mesa);
        var numerosAntes = await NumerosAsync(ficha, mesa.Gm);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha); // Turno 1

        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Passageira", 2, LinhasDeTodosOsAlvos));
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(numerosAntes, MudancasDeTodosOsAlvos), "Turno 1 de 2");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, $"/api/character-sheets/{ficha.Id}/condicoes-ativas")).Single().TurnosRestantes.Should().Be(2);

        await EncontroAsync(mesa, encounterId, "advance-turn"); // Turno 2
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(numerosAntes, MudancasDeTodosOsAlvos), "Turno 2 de 2");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, $"/api/character-sheets/{ficha.Id}/condicoes-ativas")).Single().TurnosRestantes.Should().Be(1);

        await EncontroAsync(mesa, encounterId, "advance-turn"); // Turno 3: venceu
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes, "vencido o prazo");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, $"/api/character-sheets/{ficha.Id}/condicoes-ativas")).Should().BeEmpty();
    }

    [Fact]
    public async Task Aplicada_na_preparacao_com_prazo_conta_a_partir_do_Turno_1()
    {
        var mesa = await MesaAsync("Prep");
        var ficha = await NovoPersonagemAsync(mesa);
        var antes = await NumerosAsync(ficha, mesa.Gm);
        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Antecipada", 1, Fixo("Atributo", "Forca", -4)));

        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(antes, "Turno 0");
        await EncontroAsync(mesa, encounterId, "start");
        (await NumerosAsync(ficha, mesa.Gm))["Atributo:Forca"].Should().Be(6, "Turno 1 é o único Turno do prazo");
        await EncontroAsync(mesa, encounterId, "advance-turn");
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(antes, "Turno 2: venceu");
    }

    [Fact]
    public async Task Remover_o_participante_ou_apagar_o_encontro_desfaz_tudo()
    {
        var mesa = await MesaAsync("Del");
        var ficha = await NovoPersonagemAsync(mesa);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Presa", null, LinhasDeTodosOsAlvos));
        (await RetratoAsync(ficha, mesa.Gm)).Should().NotEqual(retratoAntes);

        await EnviarAsync(HttpMethod.Delete, $"/api/encounters/{encounterId}/participants/{participantId}", mesa.Gm);

        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
    }

    [Fact]
    public async Task Dois_encontros_em_andamento_somam_as_condicoes_da_mesma_ficha()
    {
        var mesa = await MesaAsync("Dois");
        var ficha = await NovoPersonagemAsync(mesa);
        var antes = await NumerosAsync(ficha, mesa.Gm);
        var (encontroA, participanteA) = await EncontroEmAndamentoAsync(mesa, ficha);
        var (encontroB, participanteB) = await EncontroEmAndamentoAsync(mesa, ficha);

        await AplicarAsync(mesa, encontroA, participanteA, await CriarCondicaoAsync(mesa, "Ferido", null, Fixo("Atributo", "Forca", -2)));
        await AplicarAsync(mesa, encontroB, participanteB, await CriarCondicaoAsync(mesa, "Cansado", null, Fixo("Atributo", "Forca", -3)));

        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(antes, ("Atributo:Forca", 5), ("Pericia", 5), ("Maestria:Espadas", 8)));
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, $"/api/character-sheets/{ficha.Id}/condicoes-ativas"))
            .Select(c => c.Nome).Should().BeEquivalentTo($"Ferido {mesa.Tag}", $"Cansado {mesa.Tag}");

        await EncontroAsync(mesa, encontroA, "finish");
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(Com(antes, ("Atributo:Forca", 7), ("Pericia", 7), ("Maestria:Espadas", 10)));
    }

    // ---------- NPC e Criatura ----------

    [Fact]
    public async Task Npc_concedido_se_comporta_como_personagem()
    {
        var mesa = await MesaAsync("NpcC");
        var ficha = await NovaFichaDoGmAsync(mesa, Npc, concedida: true);
        var numerosAntes = await NumerosAsync(ficha, mesa.Gm);
        numerosAntes.Should().Contain("Maximo:estresse", 10).And.Contain("Afinidade:Fogo", 6).And.Contain("Maestria:Espadas", 13).And.Contain("Sub:eficienciaElemental", 3);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);

        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Tudo", null, LinhasDeTodosOsAlvos));

        var durante = Com(numerosAntes, MudancasDeTodosOsAlvos);
        (await NumerosAsync(ficha, mesa.Gm)).Should().Equal(durante);
        (await NumerosAsync(ficha, mesa.Jogador)).Should().Equal(durante, "o dono vê os mesmos números");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, $"/api/npc-sheets/{ficha.Id}/condicoes-ativas")).Single().Nome.Should().Be($"Tudo {mesa.Tag}");

        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);
        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
    }

    [Fact]
    public async Task Npc_concedido_cada_alvo_com_fixo_e_percentual()
    {
        var mesa = await MesaAsync("NpcL");
        var ficha = await NovaFichaDoGmAsync(mesa, Npc, concedida: true);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Pct("Atributo", "Forca", -50), ("Atributo:Forca", 10, 5), ("Pericia", 10, 5), ("Maestria:Espadas", 13, 8)),
            Caso(Fixo("Atributo", "Vigor", 2), ("Atributo:Vigor", 8, 10), ("Sub:defesaNatural", 8, 10), ("Maximo:vitalidade", 16, 20)),
            Caso(Fixo("Pericia", ficha.Pericia, 2), ("Pericia", 10, 12)),
            Caso(Pct("Pericia", null, -25), ("Pericia", 10, 7)),
            Caso(Fixo("SubAtributo", "Esquiva Natural", -2), ("Sub:esquivaNatural", 6, 4)),
            Caso(Pct("SubAtributo", "Redução Física", -50), ("Sub:reducaoFisica", 4, 2)),
            Caso(Fixo("Dano", "Cortante", 3), ("Dano:cortante", 10, 13)),
            Caso(Pct("Dano", null, -50), ("Dano:cortante", 10, 5)),
            Caso(Fixo("RecursoTotal", "Foco", -20), ("Maximo:foco", 14, 0)),
            Caso(Pct("RecursoTotal", "Adrenalina", -30), ("Maximo:adrenalina", 10, 7)),
            Caso(Fixo("AfinidadeElemental", "Fogo", 2), ("Afinidade:Fogo", 6, 8), ("Sub:eficienciaElemental", 3, 4), ("Sub:danoElemental", 3, 4)),
            Caso(Pct("AfinidadeElemental", null, -50), ("Afinidade:Fogo", 6, 3), ("Sub:eficienciaElemental", 3, 2), ("Sub:danoElemental", 3, 1)),
            Caso(Fixo("DanoElemental", "Fogo", 4), ("DanoElemental:Fogo", 0, 4)),
            Caso(Fixo("Maestria", ficha.Pericia, 2), ("Maestria:Espadas", 13, 15)),
            Caso(Pct("Maestria", null, -50), ("Maestria:Espadas", 13, 6)));
    }

    [Fact]
    public async Task Criatura_concedida_se_comporta_como_personagem()
    {
        var mesa = await MesaAsync("CriC");
        var ficha = await NovaFichaDoGmAsync(mesa, Criatura, concedida: true);

        await ConferirLinhasAsync(mesa, ficha,
            Caso(Pct("Atributo", "Forca", -50), ("Atributo:Forca", 10, 5), ("Pericia", 10, 5), ("Maestria:Espadas", 13, 8)),
            Caso(Fixo("Atributo", "Vigor", 2), ("Atributo:Vigor", 8, 10), ("Sub:defesaNatural", 8, 10), ("Maximo:vitalidade", 16, 20)),
            Caso(Pct("Atributo", "Agilidade", -50), ("Atributo:Agilidade", 6, 3), ("Sub:iniciativa", 6, 3), ("Sub:movimentacao", 12, 6), ("Sub:esquivaNatural", 6, 3)),
            Caso(Fixo("Pericia", ficha.Pericia, 2), ("Pericia", 10, 12)),
            Caso(Pct("Pericia", null, -25), ("Pericia", 10, 7)),
            Caso(Fixo("SubAtributo", "Esquiva Natural", -2), ("Sub:esquivaNatural", 6, 4)),
            Caso(Pct("SubAtributo", "Redução Física", -50), ("Sub:reducaoFisica", 4, 2)),
            Caso(Fixo("Dano", "Cortante", 3), ("Dano:cortante", 10, 13)),
            Caso(Pct("Dano", null, -50), ("Dano:cortante", 10, 5)),
            Caso(Fixo("RecursoTotal", "Foco", -20), ("Maximo:foco", 14, 0)),
            Caso(Pct("RecursoTotal", "Adrenalina", -30), ("Maximo:adrenalina", 10, 7)),
            Caso(Fixo("DanoElemental", "Fogo", 4), ("DanoElemental:Fogo", 0, 4)),
            Caso(Fixo("Maestria", ficha.Pericia, 2), ("Maestria:Espadas", 13, 15)),
            Caso(Pct("Maestria", null, -50), ("Maestria:Espadas", 13, 6)),
            // A Criatura não tem Estresse nem lista de Afinidades: estas linhas não têm onde agir.
            Caso(Fixo("RecursoTotal", "Estresse", -5)),
            Caso(Fixo("AfinidadeElemental", null, 5)));

        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Vista", null, Fixo("Atributo", "Forca", -1)));
        (await NumerosAsync(ficha, mesa.Jogador))["Atributo:Forca"].Should().Be(9, "o dono vê os mesmos números");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, $"/api/creature-sheets/{ficha.Id}/condicoes-ativas")).Single().Linhas.Should().Equal("Força −1");
    }

    [Theory]
    [InlineData(Npc, "NpcGm")]
    [InlineData(Criatura, "CriGm")]
    public async Task Ficha_do_GM_nao_muda_enquanto_tres_instancias_dela_carregam_condicoes_diferentes(string rota, string tag)
    {
        var mesa = await MesaAsync(tag);
        var ficha = await NovaFichaDoGmAsync(mesa, rota, concedida: false);
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);

        var encounterId = await NovoEncontroAsync(mesa);
        var instancias = new[] { await ParticiparAsync(mesa, encounterId, ficha), await ParticiparAsync(mesa, encounterId, ficha), await ParticiparAsync(mesa, encounterId, ficha) };
        await EncontroAsync(mesa, encounterId, "start");
        await AplicarAsync(mesa, encounterId, instancias[0], await CriarCondicaoAsync(mesa, "Tudo", null, LinhasDeTodosOsAlvos));
        await AplicarAsync(mesa, encounterId, instancias[1], await CriarCondicaoAsync(mesa, "Fraco", null, Pct("Atributo", "Forca", -50), Fixo("RecursoTotal", "Vitalidade", -10)));
        await AplicarAsync(mesa, encounterId, instancias[2], await CriarCondicaoAsync(mesa, "Forte", null, Fixo("Atributo", "Forca", 5), Fixo("Dano", null, 3)));

        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, $"/api/{rota}/{ficha.Id}/condicoes-ativas")).Should().BeEmpty();
    }

    // ---------- o que NÃO olha para as Condições ----------

    [Fact]
    public async Task Orcamentos_de_pontos_ignoram_as_condicoes()
    {
        var mesa = await MesaAsync("Orc");
        var ficha = await NovoPersonagemAsync(mesa);
        var raiz = $"/api/character-sheets/{ficha.Id}";
        var orcamentoDeAtributos = await TextoAsync(mesa.Gm, $"{raiz}/attributes/budget");
        var orcamentoDePericias = await TextoAsync(mesa.Gm, $"{raiz}/skills/budget");

        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Travado", null,
            Fixo("Atributo", "Destreza", 20), Pct("Atributo", "Forca", -100), Fixo("Pericia", null, -20), Fixo("Maestria", null, 5)));
        (await NumerosAsync(ficha, mesa.Gm))["Atributo:Destreza"].Should().Be(20, "a Condição está valendo");

        (await TextoAsync(mesa.Gm, $"{raiz}/attributes/budget")).Should().Be(orcamentoDeAtributos);
        (await TextoAsync(mesa.Gm, $"{raiz}/skills/budget")).Should().Be(orcamentoDePericias);

        // O teto do orçamento continua o mesmo: o que cabe sem Condição cabe, um ponto a mais não.
        var atributos = JsonSerializer.Deserialize<AttributePointBudgetResponse>(orcamentoDeAtributos, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var livre = atributos.PontosDisponiveis - atributos.GastoTotal;
        (await _client.SendAsync(Authed(HttpMethod.Put, $"{raiz}/attributes/Destreza", mesa.Gm, new UpdateCharacterAttributeRequest(livre + 1, 0, false))))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await EnviarAsync(HttpMethod.Put, $"{raiz}/attributes/Destreza", mesa.Gm, new UpdateCharacterAttributeRequest(livre, 0, false), HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Requisito_de_passiva_ignora_as_condicoes_nos_dois_sentidos()
    {
        var mesa = await MesaAsync("Req");
        var ficha = await NovoPersonagemAsync(mesa); // Força 10, Agilidade 6
        var cumprida = await CriarPassivaAsync(mesa, "Braço Forte", new RequisitosDePassivaDto(Atributos: [new("Forca", 8)]));
        var pendente = await CriarPassivaAsync(mesa, "Pés Leves", new RequisitosDePassivaDto(Atributos: [new("Agilidade", 8)]));
        var jaNaFicha = (await (await EnviarAsync(HttpMethod.Post, $"/api/character-sheets/{ficha.Id}/spell-abilities", mesa.Gm,
            new AddCharacterSpellAbilityRequest(cumprida, null, null, null, null, null), HttpStatusCode.Created)).Content.ReadFromJsonAsync<CharacterSpellAbilityResponse>())!.Id;
        var disponiveisAntes = await TextoAsync(mesa.Gm, $"/api/character-sheets/{ficha.Id}/spell-abilities/passivas-disponiveis");

        // Com a Condição a ficha MOSTRA Força 4 (abaixo do requisito) e Agilidade 12 (acima do requisito).
        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Invertido", null, Fixo("Atributo", "Forca", -6), Fixo("Atributo", "Agilidade", 6)));
        var numeros = await NumerosAsync(ficha, mesa.Gm);
        numeros["Atributo:Forca"].Should().Be(4);
        numeros["Atributo:Agilidade"].Should().Be(12);

        (await TextoAsync(mesa.Gm, $"/api/character-sheets/{ficha.Id}/spell-abilities/passivas-disponiveis")).Should().Be(disponiveisAntes);
        var naFicha = await LerAsync<List<CharacterSpellAbilityResponse>>(mesa.Gm, $"/api/character-sheets/{ficha.Id}/spell-abilities");
        naFicha.Single(e => e.Id == jaNaFicha).RequisitosPendentes.Should().BeEmpty("a Força sem Condições continua 10");
        var recusada = await _client.SendAsync(Authed(HttpMethod.Post, $"/api/character-sheets/{ficha.Id}/spell-abilities", mesa.Jogador,
            new AddCharacterSpellAbilityRequest(pendente, null, null, null, null, null)));
        recusada.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a Agilidade sem Condições continua 6");
        (await recusada.Content.ReadAsStringAsync()).Should().Contain("Agilidade ≥ 8");
    }

    private async Task<string> CriarPassivaAsync(Mesa mesa, string nome, RequisitosDePassivaDto requisitos)
    {
        var response = await EnviarAsync(HttpMethod.Post, "/api/spell-ability-bank", mesa.Gm,
            new CreateSpellAbilityEntryRequest($"{nome} {mesa.Tag}", "Passiva", 0, "Descrição.", [], false, "Livre", requisitos));
        var id = (await response.Content.ReadFromJsonAsync<SpellAbilityEntryResponse>())!.Id;
        var anexo = (await (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{mesa.CampaignId}/attachments", mesa.Gm, new AttachToCampaignRequest(null, null, null, id, null)))
            .Content.ReadFromJsonAsync<CampaignAttachmentResponse>())!.Id;
        await EnviarAsync(HttpMethod.Put, $"/api/campaigns/{mesa.CampaignId}/attachments/{anexo}/visibility", mesa.Gm, true);
        return id;
    }

    // ---------- valores guardados ----------

    private static UpdateCharacterSheetRequest ComoPut(CharacterSheetResponse s) => new(
        s.ImageId, s.Nome, s.Linhagem, s.Variante, s.Vocacao, s.SubVocacao, s.Afinidade, s.Propriedade, s.PossuiCoracaoDeMana,
        s.ExperienciaAtual, s.EAPAtual, s.NucleosRankF, s.NucleosRankE, s.NucleosRankD, s.NucleosRankC, s.NucleosRankB, s.NucleosRankA, s.NucleosRankS,
        s.PontosDeIgnicaoAtual, s.PontosDeIgnicaoBonusManual, s.VitalidadeAtual, s.FocoAtual, s.AdrenalinaAtual, s.EstresseAtual,
        s.Cobertura, s.Ciclos, s.PontosDePericiaBonusCritico, s.ArcaRolada, s.Estrela, s.SinaAtual, s.HistoricoId, s.EquipmentKitId, s.AfinidadeAdicional);

    [Fact]
    public async Task Salvar_de_volta_o_que_a_tela_leu_sob_uma_condicao_nao_guarda_nada_dela()
    {
        var mesa = await MesaAsync("Volta");
        var ficha = await NovoPersonagemAsync(mesa);
        var raiz = $"/api/character-sheets/{ficha.Id}";
        // Atuais abaixo de todo máximo reduzido, para o limite "Atual ≤ máximo" não entrar nesta conta.
        var inicial = await LerAsync<CharacterSheetResponse>(mesa.Gm, raiz);
        await EnviarAsync(HttpMethod.Put, raiz, mesa.Jogador, ComoPut(inicial) with { VitalidadeAtual = 5, FocoAtual = 4, AdrenalinaAtual = 3, EstresseAtual = 2 });
        var retratoAntes = await RetratoAsync(ficha, mesa.Gm);
        var guardadoAntes = await GuardadoAsync(ficha);

        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Tudo", null,
            [.. LinhasDeTodosOsAlvos, Pct("RecursoTotal", "Foco", -50), Fixo("RecursoTotal", "Adrenalina", -5), Fixo("AfinidadeElemental", null, 3)]));
        (await RetratoAsync(ficha, mesa.Gm)).Should().NotEqual(retratoAntes);

        // A página aberta lê tudo sob a Condição e salva de volta, campo por campo, sem o jogador mudar nada.
        var lida = await LerAsync<CharacterSheetResponse>(mesa.Jogador, raiz);
        await EnviarAsync(HttpMethod.Put, raiz, mesa.Jogador, ComoPut(lida));
        foreach (var a in await LerAsync<List<CharacterAttributeResponse>>(mesa.Jogador, $"{raiz}/attributes"))
            await EnviarAsync(HttpMethod.Put, $"{raiz}/attributes/{a.Atributo}", mesa.Jogador, new UpdateCharacterAttributeRequest(a.Gasto, a.Bonus, a.TemMaestria));
        foreach (var p in await LerAsync<List<CharacterSkillResponse>>(mesa.Jogador, $"{raiz}/skills"))
            await EnviarAsync(HttpMethod.Put, $"{raiz}/skills/{p.Pericia}", mesa.Jogador, new UpdateCharacterSkillRequest(p.Gasto, p.AtributoEscolhido));
        foreach (var m in await LerAsync<List<CharacterMasteryResponse>>(mesa.Jogador, $"{raiz}/masteries"))
            await EnviarAsync(HttpMethod.Put, $"{raiz}/masteries/{m.Id}", mesa.Jogador, new UpdateCharacterMasteryRequest(m.Nome, m.Pericia, m.Atributo, m.GastoMaestria));
        foreach (var a in await LerAsync<List<CharacterAffinityResponse>>(mesa.Jogador, $"{raiz}/affinities"))
            await EnviarAsync(HttpMethod.Put, $"{raiz}/affinities/{a.Id}", mesa.Jogador,
                new UpdateCharacterAffinityRequest(a.Elemento, a.ElementoValor, a.SegundaEssencia, a.SegundaEssenciaValor, a.SubElementoValor, a.Experiencia));

        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);

        (await RetratoAsync(ficha, mesa.Gm)).Should().Equal(retratoAntes);
        (await GuardadoAsync(ficha)).Should().Be(guardadoAntes);
    }

    /// <summary>As linhas da ficha como estão no banco (o que uma Condição nunca pode alterar).</summary>
    private async Task<string> GuardadoAsync(Ficha f)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        var id = Guid.Parse(f.Id);
        var sheet = await db.CharacterSheets.AsNoTracking().SingleAsync(s => s.Id == id);
        var atributos = await db.CharacterAttributes.AsNoTracking().Where(a => a.CharacterSheetId == id).OrderBy(a => a.Atributo).ToListAsync();
        var pericias = await db.CharacterSkills.AsNoTracking().Where(s => s.CharacterSheetId == id).OrderBy(s => s.PericiaId).ToListAsync();
        var maestrias = await db.CharacterMasteries.AsNoTracking().Where(m => m.CharacterSheetId == id).OrderBy(m => m.Id).ToListAsync();
        var afinidades = await db.CharacterAffinities.AsNoTracking().Where(a => a.CharacterSheetId == id).OrderBy(a => a.Id).ToListAsync();
        return JsonSerializer.Serialize(new { sheet, atributos, pericias, maestrias, afinidades });
    }

    [Fact]
    public async Task Salvar_limita_o_atual_ao_maximo_com_condicoes_e_esse_corte_e_permanente()
    {
        var mesa = await MesaAsync("Corte");
        var ficha = await NovoPersonagemAsync(mesa);
        var raiz = $"/api/character-sheets/{ficha.Id}";
        var inicial = await LerAsync<CharacterSheetResponse>(mesa.Gm, raiz);
        await EnviarAsync(HttpMethod.Put, raiz, mesa.Jogador, ComoPut(inicial) with { VitalidadeAtual = 16, FocoAtual = 14, AdrenalinaAtual = 10, EstresseAtual = 10 });

        var (encounterId, participantId) = await EncontroEmAndamentoAsync(mesa, ficha);
        var aplicadaId = await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Debilitado", null,
            Fixo("RecursoTotal", "Vitalidade", -6), Pct("RecursoTotal", "Foco", -50), Fixo("RecursoTotal", "Adrenalina", 5), Fixo("RecursoTotal", "Estresse", -20)));

        // Ativar a Condição não mexe no Atual guardado (isso é da próxima tarefa); só os máximos mudam.
        var sob = await LerAsync<CharacterSheetResponse>(mesa.Jogador, raiz);
        (sob.VitalidadeMaximo, sob.FocoMaximo, sob.AdrenalinaMaximo, sob.EstresseMaximo).Should().Be((10, 7, 15, 0));
        (sob.VitalidadeAtual, sob.FocoAtual, sob.AdrenalinaAtual, sob.EstresseAtual).Should().Be((16, 14, 10, 10));

        // O jogador tenta salvar acima do total reduzido (e usa o total aumentado de Adrenalina).
        await EnviarAsync(HttpMethod.Put, raiz, mesa.Jogador, ComoPut(sob) with { AdrenalinaAtual = 15 });
        var salva = await LerAsync<CharacterSheetResponse>(mesa.Jogador, raiz);
        (salva.VitalidadeAtual, salva.FocoAtual, salva.AdrenalinaAtual, salva.EstresseAtual).Should().Be((10, 7, 15, 0));

        await RemoverAsync(mesa, encounterId, participantId, aplicadaId);
        var depois = await LerAsync<CharacterSheetResponse>(mesa.Jogador, raiz);
        (depois.VitalidadeMaximo, depois.FocoMaximo, depois.AdrenalinaMaximo, depois.EstresseMaximo).Should().Be((16, 14, 10, 10));
        // O corte foi real e fica; a Adrenalina guardada (15) está acima do total de volta a 10 até o próximo salvamento.
        (depois.VitalidadeAtual, depois.FocoAtual, depois.AdrenalinaAtual, depois.EstresseAtual).Should().Be((10, 7, 15, 0));
        await EnviarAsync(HttpMethod.Put, raiz, mesa.Jogador, ComoPut(depois));
        (await LerAsync<CharacterSheetResponse>(mesa.Jogador, raiz)).AdrenalinaAtual.Should().Be(10);
    }

    // ---------- GET condicoes-ativas ----------

    [Fact]
    public async Task Condicoes_ativas_lista_nome_descricao_icone_turnos_e_o_que_cada_uma_muda()
    {
        var mesa = await MesaAsync("Lista");
        var ficha = await NovoPersonagemAsync(mesa);
        var url = $"/api/character-sheets/{ficha.Id}/condicoes-ativas";
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, url)).Should().BeEmpty("sem encontro");

        var encounterId = await NovoEncontroAsync(mesa);
        var participantId = await ParticiparAsync(mesa, encounterId, ficha);
        var condicaoId = await CriarCondicaoAsync(mesa, "Envenenado", 3, Fixo("Atributo", "Forca", -2), Pct("Pericia", ficha.Pericia, -10), Fixo("Maestria", null, 1));
        await AplicarAsync(mesa, encounterId, participantId, condicaoId);
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, url)).Should().BeEmpty("na preparação");

        await EncontroAsync(mesa, encounterId, "start");

        var ativa = (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, url)).Single();
        ativa.Nome.Should().Be($"Envenenado {mesa.Tag}");
        ativa.Descricao.Should().Be("Descrição da condição.");
        ativa.Icone.Should().Be("Veneno");
        ativa.TurnosRestantes.Should().Be(3);
        ativa.Linhas.Should().Equal("Força −2", $"{ficha.NomeDaPericia} −10%", "Todas as Maestrias +1");
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Gm, url)).Should().BeEquivalentTo(new[] { ativa });

        // Sem prazo: TurnosRestantes nulo.
        await AplicarAsync(mesa, encounterId, participantId, await CriarCondicaoAsync(mesa, "Marcado", null, Fixo("Dano", null, 1)));
        (await LerAsync<List<CondicaoAtivaNaFichaResponse>>(mesa.Jogador, url)).Single(c => c.Nome == $"Marcado {mesa.Tag}").TurnosRestantes.Should().BeNull();
    }

    [Fact]
    public async Task Condicoes_ativas_so_para_quem_pode_editar_a_ficha()
    {
        var mesa = await MesaAsync("Acesso");
        var outraMesa = await MesaAsync("Intruso");
        var personagem = await NovoPersonagemAsync(mesa);
        var npc = await NovaFichaDoGmAsync(mesa, Npc, concedida: true);
        var criatura = await NovaFichaDoGmAsync(mesa, Criatura, concedida: true);

        foreach (var intruso in new[] { outraMesa.Gm, outraMesa.Jogador })
        {
            // O mesmo resultado dos demais GETs calculados de cada controller.
            (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/character-sheets/{personagem.Id}/condicoes-ativas", intruso))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/npc-sheets/{npc.Id}/condicoes-ativas", intruso))).StatusCode.Should().Be(HttpStatusCode.NotFound);
            (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/creature-sheets/{criatura.Id}/condicoes-ativas", intruso))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        (await _client.GetAsync($"/api/character-sheets/{personagem.Id}/condicoes-ativas")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _client.SendAsync(Authed(HttpMethod.Get, $"/api/character-sheets/{Guid.NewGuid()}/condicoes-ativas", mesa.Gm))).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
