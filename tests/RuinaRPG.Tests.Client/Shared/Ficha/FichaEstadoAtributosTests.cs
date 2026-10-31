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

    private const string Afinidades = """{"gasto":3,"maximo":6,"secaoVisivel":true,"elementos":[{"id":2,"nome":"Fogo","tipo":"Base","teto":null,"pontos":2,"experiencia":1,"bloqueado":false,"motivo":null,"pontosComCondicoes":2},{"id":4,"nome":"Gelo","tipo":"SubElemento","teto":null,"pontos":1,"experiencia":null,"bloqueado":true,"motivo":"Precisa de 1 em Água e 1 em Ar","pontosComCondicoes":1}]}""";

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
                : caminho.EndsWith("/afinidades") ? (HttpStatusCode.OK, Afinidades)
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
            lista.Add($"GET {K(tipo)}/afinidades");
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
        if (tipo == "Criatura")
            estado.Afinidades.Should().BeNull();
        else
            estado.Afinidades!.Elementos.Select(e => e.Id).Should().Equal(2, 4);
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
        _respostas[$"GET {K(tipo)}/afinidades"] = (HttpStatusCode.OK, "null");

        await estado.CarregarAtributosECombateAsync();

        estado.Atributos.Should().BeEmpty();
        estado.Pericias.Should().BeEmpty();
        estado.Afinidades.Should().BeNull();
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
    public async Task The_reload_reads_the_points_per_element(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarAtributosECombateAsync();

        estado.Afinidades!.Gasto.Should().Be(3);
        estado.Afinidades.Maximo.Should().Be(6);
        estado.Afinidades.SecaoVisivel.Should().BeTrue();
        estado.Afinidades.Elementos.Select(e => (e.Id, e.Nome, e.Pontos, e.Experiencia, e.Bloqueado)).Should().Equal(
            (2, "Fogo", 2, (int?)1, false), (4, "Gelo", 1, (int?)null, true));
    }

    [Theory]
    [InlineData("Personagem", 1, """{"pontos":3,"experiencia":1}""")]
    [InlineData("NPC", -1, """{"pontos":1,"experiencia":1}""")]
    public async Task Changing_the_points_puts_them_with_the_experience_then_reloads(string tipo, int delta, string corpo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        estado.Erro = "antigo";
        var notificacoes = 0;
        estado.Alterado += () => notificacoes++;

        await estado.MudarPontosDaAfinidadeAsync(2, delta);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/afinidades/2 {corpo}" }.Concat(Recarga(tipo)));
        estado.Erro.Should().BeNull();
        estado.PenalidadesVersao.Should().Be(0);
        notificacoes.Should().Be(1);
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task Changing_the_experience_puts_it_with_the_points_then_reloads(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();

        await estado.AtualizarExperienciaDaAfinidadeAsync(2, null);

        _requisicoes.Should().Equal(new[] { $"PUT {K(tipo)}/afinidades/2 {{\"pontos\":2,\"experiencia\":null}}" }.Concat(Recarga(tipo)));
    }

    [Theory]
    [MemberData(nameof(TiposComAfinidades))]
    public async Task A_refused_element_shows_the_server_message_and_still_reloads(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAtributosECombateAsync();
        _requisicoes.Clear();
        _respostas[$"PUT {K(tipo)}/afinidades/4"] = (HttpStatusCode.BadRequest, "\"Gelo depende de Ar: zere Gelo antes.\"");

        await estado.MudarPontosDaAfinidadeAsync(4, -1);

        estado.Erro.Should().Be("Gelo depende de Ar: zere Gelo antes.");
        _requisicoes.Skip(1).Should().Equal(Recarga(tipo));
    }

    // Uma recusa sem mensagem (corpo vazio, ou um texto JSON vazio) ainda aparece no alerta.
    [Theory]
    [InlineData("")]
    [InlineData("\"\"")]
    public async Task A_refusal_without_a_message_shows_the_fallback(string corpo)
    {
        var estado = Estado("Personagem");
        await estado.CarregarAtributosECombateAsync();
        _respostas[$"PUT {K("Personagem")}/afinidades/2"] = (HttpStatusCode.InternalServerError, corpo);

        await estado.MudarPontosDaAfinidadeAsync(2, 1);

        estado.Erro.Should().Be("Não foi possível gravar a afinidade.");
    }

    // Uma edição feita enquanto a anterior ainda grava espera por ela e parte dos valores já relidos: um + seguido da
    // Experiência (ou dois + seguidos) nunca manda os pontos de antes e desfaz o que o primeiro gravou.
    [Fact]
    public async Task Edits_made_while_another_is_saving_wait_and_use_the_reloaded_values()
    {
        var (estado, puts, liberar) = EstadoComServidorLento();
        await estado.CarregarAtributosECombateAsync();

        var mais = estado.MudarPontosDaAfinidadeAsync(2, 1);
        var experiencia = estado.AtualizarExperienciaDaAfinidadeAsync(2, 4);
        var outroMais = estado.MudarPontosDaAfinidadeAsync(2, 1);
        liberar();
        await Task.WhenAll(mais, experiencia, outroMais);

        puts.Should().Equal("""{"pontos":3,"experiencia":1}""", """{"pontos":3,"experiencia":4}""", """{"pontos":4,"experiencia":4}""");
        estado.Afinidades!.Elementos.Single().Pontos.Should().Be(4);
    }

    /// <summary>
    /// Um servidor que guarda os pontos e a Experiência do Fogo e só responde o primeiro PUT quando liberado; o GET de
    /// afinidades devolve o que ele guardou.
    /// </summary>
    private static (FichaEstado Estado, List<string> Puts, Action Liberar) EstadoComServidorLento()
    {
        var (pontos, experiencia) = (2, (int?)1);
        var puts = new List<string>();
        var portao = new TaskCompletionSource();
        var http = new HttpClient(new HandlerAssincrono(async req =>
        {
            var caminho = req.RequestUri!.AbsolutePath;
            if (req.Method == HttpMethod.Put)
            {
                var corpo = await req.Content!.ReadAsStringAsync();
                lock (puts)
                    puts.Add(corpo);
                if (puts.Count == 1)
                    await portao.Task;
                var json = System.Text.Json.JsonDocument.Parse(corpo).RootElement;
                pontos = json.GetProperty("pontos").GetInt32();
                experiencia = json.GetProperty("experiencia").ValueKind == System.Text.Json.JsonValueKind.Null ? null : json.GetProperty("experiencia").GetInt32();
                return Resposta("{}");
            }
            if (caminho.EndsWith("/afinidades"))
                return Resposta($$"""{"gasto":{{pontos}},"maximo":9,"secaoVisivel":true,"elementos":[{"id":2,"nome":"Fogo","tipo":"Base","teto":null,"pontos":{{pontos}},"experiencia":{{(experiencia?.ToString() ?? "null")}},"bloqueado":false,"motivo":null,"pontosComCondicoes":{{pontos}}}]}""");
            return Resposta(caminho.EndsWith("/budget") || caminho.EndsWith("/sub-attributes") || caminho.EndsWith("/modificador-de-dano") ? "{}" : "[]");
        })) { BaseAddress = new Uri("http://localhost/api/") };
        return (new FichaEstado(http, TipoDeFichaCliente.Personagem, "s1"), puts, () => portao.SetResult());
    }

    private static HttpResponseMessage Resposta(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class HandlerAssincrono(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => responder(request);
    }
}
