using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// 2.1.0 C.2: quais records o <see cref="FichaEstado"/> lê e envia segue <see cref="TipoDeFichaCliente.Formato"/>, nunca
/// uma flag de interface. Cada teste vira as flags do tipo para o lado oposto e confere que o record lido (ou enviado) é
/// o do formato: a Criatura com todas as flags do Personagem ainda passa pelo adaptador, e o Personagem com as da
/// Criatura ainda lê os records comuns. O JSON traz o campo que só o record comum tem (<c>contaHistorico</c>,
/// <c>isRacial</c>), que o adaptador da Criatura zera.
/// </summary>
public class FichaEstadoFormatoDosDadosTests
{
    private readonly List<string> _requisicoes = new();

    private const string Pericias = """[{"pericia":"Atletismo","gasto":1,"modificador":2,"atributoEscolhido":null,"total":3,"contaHistorico":true,"nome":"Atletismo","descricao":null}]""";

    private const string Caracteristicas = """
        {"positivas":[{"id":"t1","traitId":"x","nome":"Forte","descricao":"d","custo":1,"polaridade":"Positiva","especificacao":null,"requerEspecificacao":false,"isRacial":true}],
         "totalPositivas":1,"negativas":[],"totalNegativas":0,"pontosDisponiveis":2,"pontosDisponiveisNegativas":0}
        """;

    // Todas as flags de interface que variam entre o Personagem e a Criatura, viradas para o lado do outro.
    private static TipoDeFichaCliente CriaturaComAsFlagsDoPersonagem() => TipoDeFichaCliente.Criatura with
    {
        TemArmaManual = false,
        CadastraItemNoPicker = TipoDeFichaCliente.Personagem.CadastraItemNoPicker,
        TemAntecedentes = true,
        TemOrcamentoDePericias = true,
        TemLinhagemEVocacao = true,
        TemTracosRaciais = true,
    };

    private static TipoDeFichaCliente PersonagemComAsFlagsDaCriatura() => TipoDeFichaCliente.Personagem with
    {
        TemArmaManual = true,
        CadastraItemNoPicker = TipoDeFichaCliente.Criatura.CadastraItemNoPicker,
        TemAntecedentes = false,
        TemOrcamentoDePericias = false,
        TemLinhagemEVocacao = false,
        TemTracosRaciais = false,
    };

    private FichaEstado Estado(TipoDeFichaCliente tipo)
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var json = req.Method != HttpMethod.Get ? "{}"
                : caminho.EndsWith("/skills") ? Pericias
                : caminho.EndsWith("/traits") ? Caracteristicas
                : caminho.EndsWith("/weapons") || caminho.EndsWith("/armor-slots") || caminho.EndsWith("/shields") || caminho.EndsWith("/attributes") ? "[]"
                : "{}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, tipo, "s1");
    }

    [Fact]
    public async Task As_Pericias_seguem_o_Formato_e_nao_as_flags_de_interface()
    {
        var criatura = Estado(CriaturaComAsFlagsDoPersonagem());
        var personagem = Estado(PersonagemComAsFlagsDaCriatura());

        await criatura.CarregarAtributosECombateAsync();
        await personagem.CarregarAtributosECombateAsync();

        criatura.Pericias.Single().ContaHistorico.Should().BeFalse();
        personagem.Pericias.Single().ContaHistorico.Should().BeTrue();
    }

    [Fact]
    public async Task As_Caracteristicas_seguem_o_Formato_e_nao_as_flags_de_interface()
    {
        var criatura = Estado(CriaturaComAsFlagsDoPersonagem());
        var personagem = Estado(PersonagemComAsFlagsDaCriatura());

        await criatura.CarregarCaracteristicasAsync();
        await personagem.CarregarCaracteristicasAsync();

        criatura.Caracteristicas!.Positivas.Single().IsRacial.Should().BeFalse();
        personagem.Caracteristicas!.Positivas.Single().IsRacial.Should().BeTrue();
    }

    [Fact]
    public async Task Adicionar_arma_envia_o_pedido_do_Formato_e_nao_o_da_flag_da_arma_manual()
    {
        var criatura = Estado(CriaturaComAsFlagsDoPersonagem());
        criatura.FormularioDeArma.Origem = "Catalogo";
        criatura.FormularioDeArma.ItemId = "i1";
        var personagem = Estado(PersonagemComAsFlagsDaCriatura());
        personagem.FormularioDeArma.ItemId = "i1";

        await criatura.AdicionarArmaAsync();
        await personagem.AdicionarArmaAsync();

        var posts = _requisicoes.Where(r => r.StartsWith("POST")).ToList();
        posts.Should().HaveCount(2);
        posts[0].Should().Contain("creature-sheets/s1/weapons").And.Contain("manualNome");
        posts[1].Should().Contain("character-sheets/s1/weapons").And.NotContain("manualNome");
    }
}
