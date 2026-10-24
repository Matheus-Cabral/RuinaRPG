using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// As partes do <see cref="FichaEstado"/> do alto da página (E, Task 13): o aviso de Novo Nível (os três tipos) e a
/// escolha pendente da Característica Racial (onde o tipo tem Traços Raciais), com as requisições e as recargas de hoje
/// e um aviso à página por ação.
/// </summary>
public class FichaEstadoCabecalhoTests
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    public static TheoryData<string> TiposComTracosRaciais => new() { "Personagem", "NPC" };

    private static TipoDeFichaCliente Tipo(string tipo) => tipo switch
    {
        "Personagem" => TipoDeFichaCliente.Personagem,
        "NPC" => TipoDeFichaCliente.Npc,
        _ => TipoDeFichaCliente.Criatura,
    };

    private static string K(string tipo) => Tipo(tipo).Url("s1");

    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    private const string Pendente = """{"pending":true,"gratuita":[{"traitNome":"Ágil","especificacao":null,"requerEspecificacao":false}],"obrigatoria":[]}""";

    private const string SemPendencia = """{"pending":false,"gratuita":[],"obrigatoria":[]}""";

    private FichaEstado Estado(string tipo)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : caminho.EndsWith("/level-up-notice") ? (HttpStatusCode.OK, """{"bonusTexts":["+1 Vigor"]}""")
                : caminho.EndsWith("/racial-traits/pending") ? (HttpStatusCode.OK, Pendente)
                : caminho.EndsWith("/traits") ? (HttpStatusCode.OK, """{"positivas":[],"totalPositivas":0,"negativas":[],"totalNegativas":0,"pontosDisponiveis":0,"pontosDisponiveisNegativas":0}""")
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, Tipo(tipo), "s1");
    }

    private static int Avisos(FichaEstado estado, Func<Task> acao)
    {
        var avisos = 0;
        estado.Alterado += () => avisos++;
        acao().GetAwaiter().GetResult();
        return avisos;
    }

    // ---------------- Novo Nível ----------------

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Carregar_aviso_de_novo_nivel_reads_the_bonuses(string tipo)
    {
        var estado = Estado(tipo);

        await estado.CarregarAvisoDeNovoNivelAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/level-up-notice");
        estado.BonusDeNovoNivel.Should().Equal("+1 Vigor");
    }

    // Sem aviso (404) a lista fica como estava, como hoje.
    [Fact]
    public async Task Without_a_notice_the_bonuses_stay_as_they_were()
    {
        var estado = Estado("NPC");
        _respostas[$"GET {K("NPC")}/level-up-notice"] = (HttpStatusCode.NotFound, "");

        await estado.CarregarAvisoDeNovoNivelAsync();

        estado.BonusDeNovoNivel.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Closing_the_notice_dismisses_it_on_the_server_clears_it_and_notifies_once(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarAvisoDeNovoNivelAsync();
        _requisicoes.Clear();

        Avisos(estado, estado.FecharAvisoDeNovoNivelAsync).Should().Be(1);

        _requisicoes.Should().Equal($"POST {K(tipo)}/dismiss-level-up-notice");
        estado.BonusDeNovoNivel.Should().BeEmpty();
    }

    // ---------------- Característica Racial pendente ----------------

    [Theory]
    [MemberData(nameof(TiposComTracosRaciais))]
    public async Task Loading_a_pending_choice_opens_the_dialog_with_nothing_chosen(string tipo)
    {
        var estado = Estado(tipo);
        estado.GratuitaEscolhida = "x";
        estado.ObrigatoriaEscolhida = "y";
        estado.EspecificacaoDaGratuita = "z";
        estado.EspecificacaoDaObrigatoria = "w";

        await estado.CarregarEscolhaRacialPendenteAsync();

        _requisicoes.Should().Equal($"GET {K(tipo)}/racial-traits/pending");
        estado.EscolhaRacialAberta.Should().BeTrue();
        estado.EscolhaRacialPendente!.Gratuita.Select(o => o.TraitNome).Should().Equal("Ágil");
        new[] { estado.GratuitaEscolhida, estado.ObrigatoriaEscolhida, estado.EspecificacaoDaGratuita, estado.EspecificacaoDaObrigatoria }
            .Should().AllBe(null);
    }

    [Fact]
    public async Task Without_a_pending_choice_the_dialog_stays_closed()
    {
        var estado = Estado("Personagem");
        _respostas[$"GET {K("Personagem")}/racial-traits/pending"] = (HttpStatusCode.OK, SemPendencia);

        await estado.CarregarEscolhaRacialPendenteAsync();

        estado.EscolhaRacialAberta.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(TiposComTracosRaciais))]
    public async Task Confirming_posts_the_choice_then_reloads_the_pending_choice_and_the_traits_and_notifies_once(string tipo)
    {
        var estado = Estado(tipo);
        await estado.CarregarEscolhaRacialPendenteAsync();
        estado.GratuitaEscolhida = "Agil";
        estado.EspecificacaoDaObrigatoria = "Ferreiro";
        _requisicoes.Clear();
        _respostas[$"GET {K(tipo)}/racial-traits/pending"] = (HttpStatusCode.OK, SemPendencia);

        Avisos(estado, estado.ConfirmarEscolhaRacialAsync).Should().Be(1);

        _requisicoes.Should().Equal(
            $"POST {K(tipo)}/racial-traits/resolve {{\"gratuitaTraitNome\":\"Agil\",\"obrigatoriaTraitNome\":null,\"gratuitaEspecificacao\":null,\"obrigatoriaEspecificacao\":\"Ferreiro\"}}",
            $"GET {K(tipo)}/racial-traits/pending",
            $"GET {K(tipo)}/traits");
        estado.EscolhaRacialAberta.Should().BeFalse();
        estado.Caracteristicas.Should().NotBeNull();
    }

    [Theory]
    [InlineData("\"Variante sem escolha.\"", "Variante sem escolha.")]
    [InlineData("", "Não foi possível registrar a Característica Racial escolhida.")]
    public async Task A_refused_choice_keeps_the_dialog_open_shows_the_error_and_reloads_nothing(string corpo, string erro)
    {
        var estado = Estado("Personagem");
        await estado.CarregarEscolhaRacialPendenteAsync();
        estado.GratuitaEscolhida = "Agil";
        _requisicoes.Clear();
        _respostas[$"POST {K("Personagem")}/racial-traits/resolve"] = (HttpStatusCode.BadRequest, corpo);

        Avisos(estado, estado.ConfirmarEscolhaRacialAsync).Should().Be(1);

        _requisicoes.Should().ContainSingle().Which.Should().StartWith("POST ");
        estado.Erro.Should().Be(erro);
        estado.EscolhaRacialAberta.Should().BeTrue();
        estado.GratuitaEscolhida.Should().Be("Agil");
    }

    [Fact]
    public async Task Confirming_without_a_free_trait_does_nothing()
    {
        var estado = Estado("NPC");
        await estado.CarregarEscolhaRacialPendenteAsync();
        _requisicoes.Clear();

        Avisos(estado, estado.ConfirmarEscolhaRacialAsync).Should().Be(0);

        _requisicoes.Should().BeEmpty();
        estado.EscolhaRacialAberta.Should().BeTrue();
    }

    [Fact]
    public async Task Deciding_later_closes_the_dialog_without_requests()
    {
        var estado = Estado("NPC");
        await estado.CarregarEscolhaRacialPendenteAsync();
        _requisicoes.Clear();

        Avisos(estado, () => { estado.AdiarEscolhaRacial(); return Task.CompletedTask; }).Should().Be(1);

        estado.EscolhaRacialAberta.Should().BeFalse();
        _requisicoes.Should().BeEmpty();
    }
}
