using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Atributos &amp; Perícias do <see cref="FichaEstado"/> (E, Task 11): a recarga das abas 2 e 3 (as mesmas
/// requisições do antigo <c>LoadTabs2And3Async</c>, na mesma ordem, com o Combate no fim), as edições de
/// atributo, perícia e afinidade com a recarga e a versão das penalidades, e os erros do servidor no alerta da página.
/// </summary>
public class FichaEstadoAtributosTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComAfinidades => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    private const string Atributos = """[{"atributo":"Vigor","gasto":2,"bonus":1,"temMaestria":false,"total":3}]""";

    // A Criatura não tem contaHistorico no record dela: mesmo que viesse no JSON, o Modificador fica sem negrito.
    private const string Pericias = """[{"pericia":"Atletismo","gasto":2,"modificador":1,"atributoEscolhido":"Vigor","total":3,"contaHistorico":true,"nome":"Atletismo","descricao":"Correr."}]""";

    private const string Afinidades = """[{"id":"afi1","elemento":"Fogo","elementoValor":2,"segundaEssencia":"Vida","segundaEssenciaValor":1,"subElemento":"Curar","subElementoValor":3,"experiencia":1,"elementoValorComCondicoes":2}]""";

    // Chamado a cada requisição, com o caminho (para ver o estado no meio da recarga).
    private Action<string>? _aoPedir;

    private FichaEstado Estado(string tipo)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            _aoPedir?.Invoke(caminho);
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : req.Method != HttpMethod.Get ? (HttpStatusCode.OK, "{}")
                : caminho.EndsWith("/weapons") || caminho.EndsWith("/armor-slots") || caminho.EndsWith("/shields") ? (HttpStatusCode.OK, "[]")
                : caminho.EndsWith("/attributes") ? (HttpStatusCode.OK, Atributos)
                : caminho.EndsWith("/attributes/budget") ? (HttpStatusCode.OK, """{"gastoTotal":2,"pontosDisponiveis":10}""")
                : caminho.EndsWith("/skills") ? (HttpStatusCode.OK, Pericias)
                : caminho.EndsWith("/skills/budget") ? (HttpStatusCode.OK, """{"gastoTotal":2,"pontosDisponiveis":8}""")
                : caminho.EndsWith("/affinities") ? (HttpStatusCode.OK, Afinidades)
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1");
    }

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    /// <summary>O antigo LoadTabs2And3Async: em sequência, o que o tipo tem, e depois o Combate.</summary>
    private static string[] Recarga(string tipo)
    {
        var lista = new List<string> { $"GET {K(tipo)}/attributes", $"GET {K(tipo)}/attributes/budget", $"GET {K(tipo)}/skills" };
        if (tipo == "Personagem")
            lista.Add($"GET {K(tipo)}/skills/budget");
        if (tipo != "Criatura")
            lista.Add($"GET {K(tipo)}/affinities");
        lista.AddRange(new[] { $"GET {K(tipo)}/weapons", $"GET {K(tipo)}/armor-slots", $"GET {K(tipo)}/shields", $"GET {K(tipo)}/sub-attributes", $"GET {K(tipo)}/modificador-de-dano" });
        return lista.ToArray();
    }

    // ---------------- carga ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_reload_reads_todays_requests_in_todays_order_then_the_combat(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarAtributosECombateAsync();

        _requisicoes.Should().Equal(Recarga(tipo));
        estado.Atributos.Select(a => (a.Atributo, a.Total)).Should().Equal(("Vigor", 3));
        estado.OrcamentoDeAtributos!.PontosDisponiveis.Should().Be(10);
        estado.Pericias.Select(p => (p.Pericia, p.Nome, p.Total)).Should().Equal(("Atletismo", "Atletismo", (int?)3));
        estado.OrcamentoDePericias?.PontosDisponiveis.Should().Be(tipo == "Personagem" ? 8 : null);
        if (tipo == "Personagem")
            estado.OrcamentoDePericias.Should().NotBeNull();
        else
            estado.OrcamentoDePericias.Should().BeNull();
        estado.Afinidades.Select(a => a.Id).Should().Equal(tipo == "Criatura" ? Array.Empty<string>() : new[] { "afi1" });
    }

    [Theory]
    [InlineData("Personagem", true)]
    [InlineData("NPC", true)]
    [InlineData("Criatura", false)]
    public async Task Only_a_sheet_with_historico_has_a_bold_skill_modifier(string tipo, bool contaHistorico)
    {
        var estado = Estado(tipo);

        await estado.CarregarAtributosECombateAsync();

        estado.Pericias.Single().ContaHistorico.Should().Be(contaHistorico);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Missing_lists_become_empty(string tipo)
    {
        var estado = Estado(tipo);
        _respostas[$"GET {K(tipo)}/attributes"] = (HttpStatusCode.OK, "null");
        _respostas[$"GET {K(tipo)}/skills"] = (HttpStatusCode.OK, "null");
        _respostas[$"GET {K(tipo)}/affinities"] = (HttpStatusCode.OK, "null");

        await estado.CarregarAtributosECombateAsync();

        estado.Atributos.Should().BeEmpty();
        estado.Pericias.Should().BeEmpty();
        estado.Afinidades.Should().BeEmpty();
    }

    // ---------------- atributos ----------------

    [Theory]
    [InlineData("Gasto", "4", """{"gasto":4,"bonus":1,"temMaestria":false}""")]
    [InlineData("Gasto", "x", """{"gasto":2,"bonus":1,"temMaestria":false}""")]
    [InlineData("Bonus", "3", """{"gasto":2,"bonus":3,"temMaestria":false}""")]
    [InlineData("TemMaestria", true, """{"gasto":2,"bonus":1,"temMaestria":true}""")]
    public async Task An_attribute_edit_puts_the_row_and_reloads_then_bumps_the_penalties(string campo, object valor, string corpo)
    {
        foreach (var tipo in new[] { "Personagem", "NPC", "Criatura" })
        {
            _requisicoes.Clear();
            var estado = Estado(tipo);
            await estado.CarregarAtributosECombateAsync();
            _requisicoes.Clear();
            estado.Erro = "antigo";
            var notificacoes = 0;
            var versaoNaRecarga = -1;
            _aoPedir = caminho => { if (caminho.EndsWith("/weapons")) versaoNaRecarga = estado.PenalidadesVersao; };
            estado.Alterado += () => notificacoes++;

            await estado.AtualizarAtributoAsync("Vigor", campo, valor);

            _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/attributes/Vigor {corpo}" }.Concat(Recarga(tipo)));
            estado.Erro.Should().BeNull();
            versaoNaRecarga.Should().Be(0);
            estado.PenalidadesVersao.Should().Be(1);
            notificacoes.Should().Be(1);
        }
    }

    // Diferença A6: o servidor recusa e a mensagem dele vai para o alerta da página, em todo tipo; a recarga vem igual.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_attribute_shows_the_server_message_and_still_reloads(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        _respostas[$"PUT {K(tipo)}/attributes/Vigor"] = (HttpStatusCode.BadRequest, "\"Acima do orçamento.\"");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.AtualizarAtributoAsync("Vigor", "Gasto", "40");

        estado.Erro.Should().Be("Acima do orçamento.");
        _requisicoes.Skip(1).Should().Equal(Recarga(tipo));
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes.Should().Be(1);
    }

    // ---------------- perícias ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_skill_gasto_edit_keeps_the_attribute_and_reloads(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        estado.Erro = "antigo";
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.AtualizarGastoDaPericiaAsync("Atletismo", "5");

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/skills/Atletismo {{\"gasto\":5,\"atributoEscolhido\":\"Vigor\"}}" }.Concat(Recarga(tipo)));
        estado.Erro.Should().BeNull();
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes.Should().Be(1);
    }

    [Fact]
    public async Task An_unparsable_skill_gasto_sends_the_current_one()
    {
        var estado = Estado("NPC");
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();

        await estado.AtualizarGastoDaPericiaAsync("Atletismo", null);

        _requisicoes.First().Should().Be($"PUT {K("NPC")}/skills/Atletismo {{\"gasto\":2,\"atributoEscolhido\":\"Vigor\"}}");
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task A_refused_skill_shows_the_server_message(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _respostas[$"PUT {K(tipo)}/skills/Atletismo"] = (HttpStatusCode.BadRequest, "\"Perícia acima do máximo.\"");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.AtualizarGastoDaPericiaAsync("Atletismo", "40");

        estado.Erro.Should().Be("Perícia acima do máximo.");
        notificacoes.Should().Be(1);
    }

    [Theory]
    [InlineData("Personagem", "", "null")]
    [InlineData("NPC", null, "null")]
    [InlineData("Criatura", "Agilidade", "\"Agilidade\"")]
    public async Task A_skill_attribute_edit_keeps_the_gasto_reloads_and_clears_the_error(string tipo, string? valor, string json)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        estado.Erro = "antigo";
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.AtualizarAtributoDaPericiaAsync("Atletismo", valor);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/skills/Atletismo {{\"gasto\":2,\"atributoEscolhido\":{json}}}" }.Concat(Recarga(tipo)));
        estado.Erro.Should().BeNull(); // 2.1.0: como as outras edições, um salvamento certo apaga o erro
        estado.PenalidadesVersao.Should().Be(1);
        notificacoes.Should().Be(1);
    }

    // ---------------- afinidades ----------------

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Adding_an_afinidade_posts_resets_the_row_and_reloads(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        var form = estado.FormularioDeAfinidade;
        (form.Elemento, form.ElementoValor, form.SegundaEssencia, form.SegundaEssenciaValor, form.SubElementoValor, form.Experiencia) = ("Fogo", 3, "Terra", 1, 2, 4);

        await estado.AdicionarAfinidadeAsync();

        _requisicoes.Should().Equal(new[]
        {
            $"POST {K(tipo)}/affinities {{\"elemento\":\"Fogo\",\"elementoValor\":3,\"segundaEssencia\":\"Terra\",\"segundaEssenciaValor\":1,\"subElementoValor\":2,\"experiencia\":4}}",
        }.Concat(Recarga(tipo)));
        new object?[] { form.Elemento, form.ElementoValor, form.SegundaEssencia, form.SegundaEssenciaValor, form.SubElementoValor, form.Experiencia }
            .Should().AllSatisfy(v => v.Should().BeNull());
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes.Should().Be(1);
    }

    [Theory]
    [InlineData("\"Essência inválida.\"", "Essência inválida.")]
    [InlineData("", "Não foi possível adicionar a afinidade.")]
    public async Task A_refused_afinidade_shows_the_message_keeps_the_row_and_reloads_nothing(string corpo, string mensagem)
    {
        var estado = Estado("Personagem");
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;
        _respostas[$"POST {K("Personagem")}/affinities"] = (HttpStatusCode.BadRequest, corpo);
        estado.FormularioDeAfinidade.Elemento = "Fogo";

        await estado.AdicionarAfinidadeAsync();

        estado.Erro.Should().Be(mensagem);
        _requisicoes.Should().ContainSingle();
        estado.FormularioDeAfinidade.Elemento.Should().Be("Fogo");
        notificacoes.Should().Be(1);
    }

    [Theory]
    [InlineData("Experiencia", 4, "\"elemento\":\"Fogo\",\"elementoValor\":2,\"segundaEssencia\":\"Vida\",\"segundaEssenciaValor\":1,\"subElementoValor\":3,\"experiencia\":4")]
    [InlineData("ElementoValor", 5, "\"elemento\":\"Fogo\",\"elementoValor\":5,\"segundaEssencia\":\"Vida\",\"segundaEssenciaValor\":1,\"subElementoValor\":3,\"experiencia\":1")]
    [InlineData("SubElementoValor", null, "\"elemento\":\"Fogo\",\"elementoValor\":2,\"segundaEssencia\":\"Vida\",\"segundaEssenciaValor\":1,\"subElementoValor\":null,\"experiencia\":1")]
    // Trocar a Essência 1 por uma que não cruza com a Essência 2 a descarta, e o valor dela junto.
    [InlineData("Elemento", "Agua", "\"elemento\":\"Agua\",\"elementoValor\":2,\"segundaEssencia\":null,\"segundaEssenciaValor\":null,\"subElementoValor\":3,\"experiencia\":1")]
    // Uma que cruza a mantém.
    [InlineData("Elemento", "Terra", "\"elemento\":\"Terra\",\"elementoValor\":2,\"segundaEssencia\":\"Vida\",\"segundaEssenciaValor\":1,\"subElementoValor\":3,\"experiencia\":1")]
    [InlineData("SegundaEssencia", null, "\"elemento\":\"Fogo\",\"elementoValor\":2,\"segundaEssencia\":null,\"segundaEssenciaValor\":null,\"subElementoValor\":3,\"experiencia\":1")]
    [InlineData("SegundaEssenciaValor", 4, "\"elemento\":\"Fogo\",\"elementoValor\":2,\"segundaEssencia\":\"Vida\",\"segundaEssenciaValor\":4,\"subElementoValor\":3,\"experiencia\":1")]
    public async Task An_afinidade_edit_puts_the_row_and_reloads(string campo, object? valor, string corpo)
    {
        foreach (var tipo in new[] { "Personagem", "NPC" })
        {
            _requisicoes.Clear();
            var estado = Estado(tipo);
            await estado.CarregarAtributosECombateAsync();
            _requisicoes.Clear();
            estado.Erro = "antigo";
            var notificacoes = 0;
            estado.Alterado += () => notificacoes++;

            await estado.AtualizarAfinidadeAsync("afi1", campo, valor);

            _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/affinities/afi1 {{{corpo}}}" }.Concat(Recarga(tipo)));
            estado.Erro.Should().BeNull();
            estado.PenalidadesVersao.Should().Be(0);
            notificacoes.Should().Be(1);
        }
    }

    [Fact]
    public async Task A_refused_afinidade_edit_shows_the_server_message_and_still_reloads()
    {
        var estado = Estado("NPC");
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        _respostas[$"PUT {K("NPC")}/affinities/afi1"] = (HttpStatusCode.BadRequest, "\"Valor acima do máximo.\"");

        await estado.AtualizarAfinidadeAsync("afi1", "Experiencia", 40);

        estado.Erro.Should().Be("Valor acima do máximo.");
        _requisicoes.Skip(1).Should().Equal(Recarga("NPC"));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Removing_an_afinidade_deletes_and_reloads(string tipo)
    {
        var estado = Estado(tipo);
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.RemoverAfinidadeAsync("afi1");

        _requisicoes.Should().Equal(new[] { $"DELETE {K(tipo)}/affinities/afi1" }.Concat(Recarga(tipo)));
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes.Should().Be(1);
    }

    // ---------------- a linha nova da afinidade ----------------

    [Fact]
    public void A_new_essencia_1_that_does_not_cross_the_essencia_2_drops_it_and_its_value()
    {
        var form = new AffinityFormModel { Elemento = "Fogo", SegundaEssencia = "Vida", SegundaEssenciaValor = 2 };

        form.TrocarEssencia1("Agua");

        form.Elemento.Should().Be("Agua");
        form.SegundaEssencia.Should().BeNull();
        form.SegundaEssenciaValor.Should().BeNull();
    }

    [Fact]
    public void A_new_essencia_1_that_crosses_the_essencia_2_keeps_it()
    {
        var form = new AffinityFormModel { Elemento = "Fogo", SegundaEssencia = "Vida", SegundaEssenciaValor = 2 };

        form.TrocarEssencia1("Terra");

        (form.Elemento, form.SegundaEssencia, form.SegundaEssenciaValor).Should().Be(("Terra", "Vida", (int?)2));
    }

    [Fact]
    public void Clearing_the_essencia_2_drops_its_value()
    {
        var form = new AffinityFormModel { Elemento = "Fogo", SegundaEssencia = "Vida", SegundaEssenciaValor = 2 };

        form.TrocarEssencia2(null);

        form.SegundaEssencia.Should().BeNull();
        form.SegundaEssenciaValor.Should().BeNull();
    }

    [Fact]
    public void Another_essencia_2_keeps_its_value()
    {
        var form = new AffinityFormModel { Elemento = "Fogo", SegundaEssencia = "Vida", SegundaEssenciaValor = 2 };

        form.TrocarEssencia2("Terra");

        (form.SegundaEssencia, form.SegundaEssenciaValor).Should().Be(("Terra", (int?)2));
    }

    [Theory]
    [InlineData("Fogo", "Vida", "Curar")]
    [InlineData("Agua", "Ar", "Gelo")]
    [InlineData("Fogo", null, null)]
    [InlineData(null, "Vida", null)]
    public void The_new_row_sub_elemento_is_where_the_essencias_cross(string? e1, string? e2, string? sub)
    {
        new AffinityFormModel { Elemento = e1, SegundaEssencia = e2 }.SubElemento.Should().Be(sub);
    }
}
