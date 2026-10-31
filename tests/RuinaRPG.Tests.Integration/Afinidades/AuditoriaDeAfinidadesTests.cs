using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using RuinaRPG.Contracts.Campaigns;
using RuinaRPG.Contracts.CharacterSheets;
using RuinaRPG.Contracts.Condicoes;
using RuinaRPG.Contracts.CreatureSheets;
using RuinaRPG.Contracts.Fichas;
using RuinaRPG.Contracts.Items;
using RuinaRPG.Contracts.NpcSheets;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Contracts.SpellsAndAbilities;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Infrastructure.CharacterSheets;
using RuinaRPG.Infrastructure.Persistence;
using RuinaRPG.Tests.Integration.Controllers;

namespace RuinaRPG.Tests.Integration.Afinidades;

/// <summary>
/// A Auditoria de Afinidades (Auditoria de Regras R0018–R0021): totais por Círculo, elementos, cruzamentos e
/// visibilidade por Vocação/Classe. Leitura aberta a qualquer usuário autenticado; escrita só do Auditor, com o mesmo
/// 403 (antes do 404) das outras auditorias. Os testes da classe dividem o banco: o que um teste muda no catálogo
/// semeado volta ao fim dele, e os elementos criados têm nomes próprios de cada teste.
/// </summary>
public class AuditoriaDeAfinidadesTests(ApiFixture fixture) : ApiTestBase(fixture)
{
    private const string Base = "/api/afinidades";
    private const int Terra = 0, Agua = 1, Fogo = 2, Ar = 3, Gelo = 4, Alma = 9;

    private static string Unico(string prefixo) => prefixo + Guid.NewGuid().ToString("N")[..8];

    private async Task<string> AuditorAsync()
    {
        var email = Unico("audafin") + "@teste.com";
        var token = await RegisterGmAndGetTokenAsync(Unico("AudAfin"), email);
        await GrantRulesAuditorAsync(email);
        return token;
    }

    private Task<string> OutroUsuarioAsync() => RegisterGmAndGetTokenAsync(Unico("GmAfin"), Unico("gmafin") + "@teste.com");

    private Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string url, string token, object? corpo = null) =>
        Client.SendAsync(AuthedRequest(metodo, url, token, corpo));

    private async Task<T> LerAsync<T>(string url, string token)
    {
        var response = await EnviarAsync(HttpMethod.Get, url, token);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }

    private async Task<ElementoDeAfinidadeResponse> CriarElementoAsync(string token, string nome, string tipo, int? teto = null)
    {
        var response = await EnviarAsync(HttpMethod.Post, $"{Base}/elementos", token, new SalvarElementoDeAfinidadeRequest(nome, tipo, teto));
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ElementoDeAfinidadeResponse>())!;
    }

    private async Task<CruzamentoDeAfinidadeResponse> CriarCruzamentoAsync(string token, SalvarCruzamentoDeAfinidadeRequest request)
    {
        var response = await EnviarAsync(HttpMethod.Post, $"{Base}/cruzamentos", token, request);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CruzamentoDeAfinidadeResponse>())!;
    }

    private async Task AlterarVisibilidadeAsync(string token, string origem, string chave, int elemento, bool visivel)
    {
        var response = await EnviarAsync(HttpMethod.Put, $"{Base}/visibilidade", token, new AlterarVisibilidadeDeElementoRequest(origem, chave, elemento, visivel));
        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync());
    }

    private static async Task DeveRecusarAsync(HttpResponseMessage response, HttpStatusCode status, string texto)
    {
        response.StatusCode.Should().Be(status);
        (await response.Content.ReadAsStringAsync()).Should().Contain(texto);
    }

    private async Task MudarNoBancoAsync(Func<RuinaRpgDbContext, Task> mudar)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>();
        await mudar(db);
        await db.SaveChangesAsync();
    }

    /// <summary>Um Personagem de uma campanha do GM <paramref name="gm"/>, com a Vocação dada.</summary>
    private async Task<string> PersonagemAsync(string gm, Vocacao? vocacao)
    {
        var (jogadorId, _) = await RegisterJogadorLinkedToAsync(gm, Unico("JgAfin"), Unico("jgafin") + "@teste.com");
        var campanha = await CreateCampaignAsync(gm, "Mesa das Afinidades");
        (await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campanha}/members", gm, new AddCampaignMemberRequest(jogadorId))).EnsureSuccessStatusCode();
        var criada = await EnviarAsync(HttpMethod.Post, $"/api/campaigns/{campanha}/character-sheets", gm, new CreateCharacterSheetRequest(jogadorId));
        criada.EnsureSuccessStatusCode();
        var id = (await criada.Content.ReadFromJsonAsync<CharacterSheetResponse>())!.Id;
        await MudarNoBancoAsync(async db => (await db.CharacterSheets.FindAsync(Guid.Parse(id)))!.Vocacao = vocacao);
        return id;
    }

    // ── Leitura ──

    [Theory]
    [InlineData("totais-por-circulo")]
    [InlineData("cruzamentos")]
    [InlineData("visibilidade")]
    public async Task As_listas_exigem_autenticacao(string lista) =>
        (await Client.GetAsync($"{Base}/{lista}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

    [Fact]
    public async Task Qualquer_usuario_le_os_totais_os_cruzamentos_e_a_visibilidade_semeados()
    {
        var gm = await OutroUsuarioAsync();

        var totais = await LerAsync<List<TotalDeAfinidadePorCirculoResponse>>($"{Base}/totais-por-circulo", gm);
        totais.Select(t => t.Circulo).Should().BeInAscendingOrder();
        totais.Should().Contain(new TotalDeAfinidadePorCirculoResponse(0, 3));

        var cruzamentos = await LerAsync<List<CruzamentoDeAfinidadeResponse>>($"{Base}/cruzamentos", gm);
        cruzamentos.Should().ContainSingle(c => c.SubElementoId == Gelo)
            .Which.Should().BeEquivalentTo(new { ElementoAId = Agua, MinimoA = 1, ElementoBId = Ar, MinimoB = 1 });
        cruzamentos.Should().OnlyContain(c => c.ElementoAId < c.ElementoBId);

        var visibilidade = await LerAsync<List<VisibilidadeDeElementoResponse>>($"{Base}/visibilidade", gm);
        visibilidade.Should().Contain(new VisibilidadeDeElementoResponse("Vocacao", "Feiticeiro", Alma));
        visibilidade.Should().Contain(new VisibilidadeDeElementoResponse("Vocacao", "Bruxo", Gelo));
    }

    // ── Quem pode escrever ──

    public static TheoryData<string, string> Escritas => new()
    {
        { "PUT", "totais-por-circulo/0" },
        { "PUT", "totais-por-circulo/99" },
        { "POST", "elementos" },
        { "PUT", "elementos/0" },
        { "PUT", "elementos/9999" },
        { "DELETE", "elementos/0" },
        { "DELETE", "elementos/9999" },
        { "POST", "cruzamentos" },
        { "PUT", $"cruzamentos/{Guid.Empty}" },
        { "DELETE", $"cruzamentos/{Guid.Empty}" },
        { "PUT", "visibilidade" },
    };

    private static object? CorpoDe(string rota) => rota switch
    {
        _ when rota.StartsWith("totais", StringComparison.Ordinal) => new SalvarTotalDeAfinidadePorCirculoRequest(1),
        _ when rota.StartsWith("elementos", StringComparison.Ordinal) => new SalvarElementoDeAfinidadeRequest("Proibido", "SubElemento", null),
        _ when rota.StartsWith("cruzamentos", StringComparison.Ordinal) => new SalvarCruzamentoDeAfinidadeRequest(Terra, 1, Alma, 1, Gelo),
        _ => new AlterarVisibilidadeDeElementoRequest("Vocacao", "Feiticeiro", Alma, false),
    };

    [Theory]
    [MemberData(nameof(Escritas))]
    public async Task Quem_nao_e_o_Auditor_recebe_403_em_toda_escrita(string metodo, string rota)
    {
        var gm = await OutroUsuarioAsync();

        var response = await EnviarAsync(new HttpMethod(metodo), $"{Base}/{rota}", gm, metodo == "DELETE" ? null : CorpoDe(rota));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        // Nada mudou: o Círculo 0 continua 3, Terra existe e Feiticeiro continua vendo Alma.
        (await LerAsync<List<TotalDeAfinidadePorCirculoResponse>>($"{Base}/totais-por-circulo", gm)).Should().Contain(new TotalDeAfinidadePorCirculoResponse(0, 3));
        (await LerAsync<List<ElementoDeAfinidadeResponse>>($"{Base}/catalogo", gm)).Should().Contain(e => e.Id == Terra && e.Nome == "Terra");
        (await LerAsync<List<VisibilidadeDeElementoResponse>>($"{Base}/visibilidade", gm)).Should().Contain(new VisibilidadeDeElementoResponse("Vocacao", "Feiticeiro", Alma));
    }

    [Theory]
    [InlineData("PUT", "totais-por-circulo/99")]
    [InlineData("PUT", "elementos/9999")]
    [InlineData("DELETE", "elementos/9999")]
    [InlineData("PUT", "cruzamentos/00000000-0000-0000-0000-000000000000")]
    [InlineData("DELETE", "cruzamentos/00000000-0000-0000-0000-000000000000")]
    public async Task O_Auditor_recebe_404_no_que_nao_existe(string metodo, string rota)
    {
        var auditor = await AuditorAsync();

        var response = await EnviarAsync(new HttpMethod(metodo), $"{Base}/{rota}", auditor, metodo == "DELETE" ? null : CorpoDe(rota));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Totais por Círculo ──

    [Fact]
    public async Task O_Auditor_edita_o_total_de_um_Circulo()
    {
        var auditor = await AuditorAsync();
        var antes = (await LerAsync<List<TotalDeAfinidadePorCirculoResponse>>($"{Base}/totais-por-circulo", auditor)).Single(t => t.Circulo == 2);
        try
        {
            (await EnviarAsync(HttpMethod.Put, $"{Base}/totais-por-circulo/2", auditor, new SalvarTotalDeAfinidadePorCirculoRequest(42)))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            (await LerAsync<List<TotalDeAfinidadePorCirculoResponse>>($"{Base}/totais-por-circulo", auditor))
                .Should().Contain(new TotalDeAfinidadePorCirculoResponse(2, 42));
        }
        finally
        {
            await EnviarAsync(HttpMethod.Put, $"{Base}/totais-por-circulo/2", auditor, new SalvarTotalDeAfinidadePorCirculoRequest(antes.Total));
        }
    }

    [Fact]
    public async Task Um_total_negativo_e_recusado()
    {
        var auditor = await AuditorAsync();

        await DeveRecusarAsync(await EnviarAsync(HttpMethod.Put, $"{Base}/totais-por-circulo/1", auditor, new SalvarTotalDeAfinidadePorCirculoRequest(-1)),
            HttpStatusCode.BadRequest, "O total deve ser um inteiro maior ou igual a zero.");
    }

    // ── Elementos ──

    [Fact]
    public async Task O_Auditor_cria_edita_e_remove_um_elemento()
    {
        var auditor = await AuditorAsync();
        var nome = Unico("Luz");

        var criado = await CriarElementoAsync(auditor, $"  {nome}  ", "SubElemento", 5);
        criado.Should().BeEquivalentTo(new { Nome = nome, Tipo = "SubElemento", Teto = (int?)5 });
        criado.Id.Should().BeGreaterThan(18);
        (await LerAsync<List<ElementoDeAfinidadeResponse>>($"{Base}/catalogo", auditor)).Should().ContainEquivalentOf(criado);

        (await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{criado.Id}", auditor, new SalvarElementoDeAfinidadeRequest(nome + " Sagrada", "Caminho", null)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync<List<ElementoDeAfinidadeResponse>>($"{Base}/catalogo", auditor))
            .Should().ContainEquivalentOf(new ElementoDeAfinidadeResponse(criado.Id, nome + " Sagrada", "Caminho", null));

        (await EnviarAsync(HttpMethod.Delete, $"{Base}/elementos/{criado.Id}", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerAsync<List<ElementoDeAfinidadeResponse>>($"{Base}/catalogo", auditor)).Should().NotContain(e => e.Id == criado.Id);
    }

    [Theory]
    [InlineData("Fogo", "Base", null, "Já existe um elemento chamado Fogo.")]
    [InlineData(" ", "Base", null, "O nome do elemento é obrigatório.")]
    [InlineData("Novo Teto", "SubElemento", 0, "O teto deve ser vazio (sem limite) ou um inteiro maior ou igual a 1.")]
    [InlineData("Novo Tipo", "Lendario", null, "Tipo de elemento inválido: Lendario.")]
    public async Task Um_elemento_invalido_e_recusado(string nome, string tipo, int? teto, string mensagem)
    {
        var auditor = await AuditorAsync();

        await DeveRecusarAsync(await EnviarAsync(HttpMethod.Post, $"{Base}/elementos", auditor, new SalvarElementoDeAfinidadeRequest(nome, tipo, teto)),
            HttpStatusCode.BadRequest, mensagem);
    }

    [Fact]
    public async Task O_teto_editado_vale_na_ficha()
    {
        var auditor = await AuditorAsync();
        var ficha = await PersonagemAsync(auditor, Vocacao.Feiticeiro);
        try
        {
            (await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{Terra}", auditor, new SalvarElementoDeAfinidadeRequest("Terra", "Base", 2)))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var afinidades = await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor);
            afinidades.Elementos.Single(e => e.Id == Terra).Teto.Should().Be(2);
        }
        finally
        {
            await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{Terra}", auditor, new SalvarElementoDeAfinidadeRequest("Terra", "Base", null));
        }
    }

    [Fact]
    public async Task Remover_um_elemento_usado_e_recusado_com_as_contagens()
    {
        var auditor = await AuditorAsync();
        var eter = await CriarElementoAsync(auditor, Unico("Éter"), "SubElemento");

        // Fichas: um Personagem com pontos no elemento e com ele como Afinidade (conta uma vez), um NPC e uma
        // Criatura com ele como Afinidade.
        var personagem = Guid.Parse(await PersonagemAsync(auditor, Vocacao.Feiticeiro));
        var npc = Guid.Parse((await (await EnviarAsync(HttpMethod.Post, "/api/npc-sheets", auditor)).Content.ReadFromJsonAsync<NpcSheetResponse>())!.Id);
        var criatura = Guid.Parse((await (await EnviarAsync(HttpMethod.Post, "/api/creature-sheets", auditor)).Content.ReadFromJsonAsync<CreatureSheetResponse>())!.Id);
        await MudarNoBancoAsync(async db =>
        {
            db.CharacterPontosDeAfinidade.Add(new CharacterPontosDeAfinidade { Id = Guid.NewGuid(), SheetId = personagem, ElementoId = eter.Id, Pontos = 1 });
            (await db.CharacterSheets.FindAsync(personagem))!.Afinidade = eter.Id;
            (await db.NpcSheets.FindAsync(npc))!.Afinidade = eter.Id;
            (await db.CreatureSheets.FindAsync(criatura))!.Afinidade = eter.Id;
        });

        // Condições: uma com o alvo Afinidade elemental e o dano elemental (conta uma vez), outra só com o dano.
        foreach (var (nome, alvos) in new[] { (Unico("Etéreo"), new[] { "AfinidadeElemental", "DanoElemental" }), (Unico("Rasgo"), new[] { "DanoElemental" }) })
            (await EnviarAsync(HttpMethod.Post, "/api/condicoes", auditor, new SalvarCondicaoRequest(nome, null, "Veneno", null,
                alvos.Select(a => new CondicaoModificadorDto(a, eter.Id.ToString(), "Fixo", 2, null)).ToList(), [])))
                .StatusCode.Should().Be(HttpStatusCode.Created);

        // Passivas: uma Passiva do Banco e um item com o elemento no requisito.
        (await EnviarAsync(HttpMethod.Post, "/api/spell-ability-bank", auditor,
            new CreateSpellAbilityEntryRequest(Unico("Aura"), "Passiva", 0, "d", [], false, "Livre", new RequisitosDePassivaDto(Afinidade: eter.Id))))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        (await EnviarAsync(HttpMethod.Post, "/api/items", auditor,
            new CreateItemRequest("Arma", Unico("Lâmina Etérea"), 1.5m, 50, null, "Espadas", "d", "F", "UmaMao", "2D6", 3, "19", 2, "Cortante",
                null, null, null, null, null, null, null, null, null) with { Requisitos = new RequisitosDePassivaDto(Afinidade: eter.Id) }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        var response = await EnviarAsync(HttpMethod.Delete, $"{Base}/elementos/{eter.Id}", auditor);

        await DeveRecusarAsync(response, HttpStatusCode.Conflict, "Este elemento é usado por 3 ficha(s), 2 Condição(ões) e 2 Passiva(s).");
        (await LerAsync<List<ElementoDeAfinidadeResponse>>($"{Base}/catalogo", auditor)).Should().Contain(e => e.Id == eter.Id);
    }

    [Fact]
    public async Task Remover_um_elemento_sem_uso_leva_os_cruzamentos_e_as_ligacoes_dele()
    {
        var auditor = await AuditorAsync();
        var caminho = await CriarElementoAsync(auditor, Unico("Sombra"), "Caminho");
        var sub = await CriarElementoAsync(auditor, Unico("Breu"), "SubElemento");
        var cruzamento = await CriarCruzamentoAsync(auditor, new SalvarCruzamentoDeAfinidadeRequest(Terra, 1, caminho.Id, 1, sub.Id));
        await AlterarVisibilidadeAsync(auditor, "Vocacao", "Bruxo", caminho.Id, true);
        await AlterarVisibilidadeAsync(auditor, "Classe", "Ecomante", caminho.Id, true);

        (await EnviarAsync(HttpMethod.Delete, $"{Base}/elementos/{caminho.Id}", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await LerAsync<List<CruzamentoDeAfinidadeResponse>>($"{Base}/cruzamentos", auditor)).Should().NotContain(c => c.Id == cruzamento.Id);
        (await LerAsync<List<VisibilidadeDeElementoResponse>>($"{Base}/visibilidade", auditor)).Should().NotContain(v => v.ElementoId == caminho.Id);
        // O Sub-Elemento que o cruzamento gerava fica, agora sem requisito, e pode ser removido também.
        (await EnviarAsync(HttpMethod.Delete, $"{Base}/elementos/{sub.Id}", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // ── Cruzamentos ──

    [Fact]
    public async Task Elemento_e_cruzamento_criados_aparecem_na_ficha_com_o_requisito()
    {
        var auditor = await AuditorAsync();
        var luz = await CriarElementoAsync(auditor, Unico("Aurora"), "SubElemento");
        var ficha = await PersonagemAsync(auditor, Vocacao.Feiticeiro);

        // O par chega na ordem (Alma, Terra): o banco grava (Terra, Alma) e cada mínimo vai junto do seu elemento.
        var cruzamento = await CriarCruzamentoAsync(auditor, new SalvarCruzamentoDeAfinidadeRequest(Alma, 1, Terra, 2, luz.Id));
        cruzamento.Should().BeEquivalentTo(new { ElementoAId = Terra, MinimoA = 2, ElementoBId = Alma, MinimoB = 1, SubElementoId = luz.Id });
        try
        {
            // Sem ligação, um Sub-Elemento novo não aparece para o Feiticeiro; ligado, aparece bloqueado pelo requisito.
            (await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor)).Elementos.Should().NotContain(e => e.Id == luz.Id);
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Feiticeiro", luz.Id, true);

            var elemento = (await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor)).Elementos.Single(e => e.Id == luz.Id);
            elemento.Should().BeEquivalentTo(new { Nome = luz.Nome, Tipo = "SubElemento", Bloqueado = true, Motivo = "Precisa de 2 em Terra e 1 em Alma" });

            // Editar o requisito muda a dica na ficha.
            (await EnviarAsync(HttpMethod.Put, $"{Base}/cruzamentos/{cruzamento.Id}", auditor, new SalvarCruzamentoDeAfinidadeRequest(Terra, 3, Alma, 4, luz.Id)))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor)).Elementos.Single(e => e.Id == luz.Id)
                .Motivo.Should().Be("Precisa de 3 em Terra e 4 em Alma");
        }
        finally
        {
            (await EnviarAsync(HttpMethod.Delete, $"{Base}/cruzamentos/{cruzamento.Id}", auditor)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        (await LerAsync<List<CruzamentoDeAfinidadeResponse>>($"{Base}/cruzamentos", auditor)).Should().NotContain(c => c.Id == cruzamento.Id);
    }

    [Theory]
    [InlineData(Ar, Agua, Gelo, 1, "Já existe um cruzamento de Água e Ar.")]
    [InlineData(Terra, Alma, Fogo, 1, "Fogo não é um Sub-Elemento.")]
    [InlineData(Terra, Terra, Gelo, 1, "Os dois elementos do cruzamento devem ser diferentes.")]
    [InlineData(Terra, Alma, Gelo, 0, "Os mínimos do cruzamento devem ser inteiros maiores ou iguais a 1.")]
    public async Task Um_cruzamento_invalido_e_recusado(int a, int b, int sub, int minimo, string mensagem)
    {
        var auditor = await AuditorAsync();

        await DeveRecusarAsync(await EnviarAsync(HttpMethod.Post, $"{Base}/cruzamentos", auditor, new SalvarCruzamentoDeAfinidadeRequest(a, minimo, b, 1, sub)),
            HttpStatusCode.BadRequest, mensagem);
    }

    [Fact]
    public async Task Um_Sub_Elemento_gerado_por_cruzamento_nao_muda_de_tipo()
    {
        var auditor = await AuditorAsync();

        await DeveRecusarAsync(await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{Gelo}", auditor, new SalvarElementoDeAfinidadeRequest("Gelo", "Base", null)),
            HttpStatusCode.BadRequest, "Gelo é gerado por um cruzamento");
    }

    [Fact]
    public async Task Mudar_as_regras_pela_Auditoria_deixa_a_ficha_inconsistente_sem_apagar_pontos()
    {
        var auditor = await AuditorAsync();
        var ficha = await PersonagemAsync(auditor, Vocacao.Feiticeiro);
        var sheetId = Guid.Parse(ficha);
        await MudarNoBancoAsync(db =>
        {
            foreach (var (elemento, pontos) in new[] { (Ar, 2), (Agua, 1), (Gelo, 1), (Alma, 1) })
                db.CharacterPontosDeAfinidade.Add(new CharacterPontosDeAfinidade { Id = Guid.NewGuid(), SheetId = sheetId, ElementoId = elemento, Pontos = pontos });
            return Task.CompletedTask;
        });
        var gelo = (await LerAsync<List<CruzamentoDeAfinidadeResponse>>($"{Base}/cruzamentos", auditor)).Single(c => c.SubElementoId == Gelo);
        try
        {
            // (a) Alma sai da Vocação, (b) o teto de Ar fica abaixo dos 2 pontos, (c) o requisito de Ar para Gelo sobe a 3.
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Feiticeiro", Alma, false);
            (await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{Ar}", auditor, new SalvarElementoDeAfinidadeRequest("Ar", "Base", 1)))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);
            (await EnviarAsync(HttpMethod.Put, $"{Base}/cruzamentos/{gelo.Id}", auditor, new SalvarCruzamentoDeAfinidadeRequest(Agua, 1, Ar, 3, Gelo)))
                .StatusCode.Should().Be(HttpStatusCode.NoContent);

            var afinidades = await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor);

            afinidades.Gasto.Should().Be(5);
            var porId = afinidades.Elementos.ToDictionary(e => e.Id);
            porId[Alma].Should().BeEquivalentTo(new { Pontos = 1, Bloqueado = true, Motivo = "Indisponível para esta Vocação/Classe" });
            porId[Ar].Should().BeEquivalentTo(new { Pontos = 2, Bloqueado = true, Motivo = "Acima do teto (1)" });
            porId[Gelo].Should().BeEquivalentTo(new { Pontos = 1, Bloqueado = true, Motivo = "Precisa de 1 em Água e 3 em Ar" });
            porId[Agua].Should().BeEquivalentTo(new { Pontos = 1, Bloqueado = false });
            await using var scope = Factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<RuinaRpgDbContext>().CharacterPontosDeAfinidade
                .Where(p => p.SheetId == sheetId).Sum(p => p.Pontos).Should().Be(5);
        }
        finally
        {
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Feiticeiro", Alma, true);
            await EnviarAsync(HttpMethod.Put, $"{Base}/elementos/{Ar}", auditor, new SalvarElementoDeAfinidadeRequest("Ar", "Base", null));
            await EnviarAsync(HttpMethod.Put, $"{Base}/cruzamentos/{gelo.Id}", auditor, new SalvarCruzamentoDeAfinidadeRequest(Agua, 1, Ar, 1, Gelo));
        }
    }

    // ── Visibilidade ──

    [Fact]
    public async Task Desligar_um_Caminho_da_Vocacao_o_tira_da_ficha_e_ligar_pela_Classe_o_devolve()
    {
        var auditor = await AuditorAsync();
        var ficha = await PersonagemAsync(auditor, Vocacao.Adepto);
        try
        {
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Adepto", Alma, false);
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Adepto", Alma, false); // desligar de novo não falha
            (await LerAsync<List<VisibilidadeDeElementoResponse>>($"{Base}/visibilidade", auditor))
                .Should().NotContain(new VisibilidadeDeElementoResponse("Vocacao", "Adepto", Alma));
            (await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor)).Elementos.Should().NotContain(e => e.Id == Alma);

            await MudarNoBancoAsync(async db => (await db.CharacterSheets.FindAsync(Guid.Parse(ficha)))!.SubVocacao = "Arauto");
            await AlterarVisibilidadeAsync(auditor, "Classe", "Arauto", Alma, true);
            await AlterarVisibilidadeAsync(auditor, "Classe", "Arauto", Alma, true); // ligar de novo não duplica
            (await LerAsync<List<VisibilidadeDeElementoResponse>>($"{Base}/visibilidade", auditor))
                .Where(v => v == new VisibilidadeDeElementoResponse("Classe", "Arauto", Alma)).Should().HaveCount(1);
            (await LerAsync<AfinidadesDaFichaResponse>($"/api/character-sheets/{ficha}/afinidades", auditor)).Elementos.Should().Contain(e => e.Id == Alma);
        }
        finally
        {
            await AlterarVisibilidadeAsync(auditor, "Vocacao", "Adepto", Alma, true);
            await AlterarVisibilidadeAsync(auditor, "Classe", "Arauto", Alma, false);
        }
    }

    [Theory]
    [InlineData("Vocacao", "Campeao", Alma, "Só as Vocações mágicas (Feiticeiro, Adepto e Bruxo) têm elementos ligados.")]
    [InlineData("Classe", "Clérigo", Alma, "Classe desconhecida: Clérigo.")]
    [InlineData("Vocacao", "Bruxo", Terra, "Terra é um Elemento base: aparece sempre.")]
    [InlineData("Raca", "Bruxo", Alma, "Origem inválida: Raca.")]
    public async Task Uma_ligacao_invalida_e_recusada(string origem, string chave, int elemento, string mensagem)
    {
        var auditor = await AuditorAsync();

        await DeveRecusarAsync(await EnviarAsync(HttpMethod.Put, $"{Base}/visibilidade", auditor, new AlterarVisibilidadeDeElementoRequest(origem, chave, elemento, true)),
            HttpStatusCode.BadRequest, mensagem);
    }
}
