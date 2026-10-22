using System.Net;
using System.Text;
using FluentAssertions;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Contracts.Images;
using RuinaRPG.Tests.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A parte Diário do <see cref="FichaEstado"/> (E, Task 13; só onde o tipo tem Diário): a lista, adicionar, editar,
/// cancelar e excluir uma entrada, com as requisições e as recargas de hoje, o rascunho e a edição em curso guardados
/// no estado (sobrevivem à troca de aba, como quando eram campos da página) e um aviso à página por ação.
/// </summary>
public class FichaEstadoDiarioTests
{
    private readonly List<string> _requisicoes = new();
    private readonly Dictionary<string, (HttpStatusCode Status, string Corpo)> _respostas = new();

    private const string Entradas = """[{"id":"d1","texto":"Chegamos à vila.","createdAt":"2026-10-01T12:00:00Z","imageUrls":["/img/dois.png","/img/fora.png"]}]""";

    private const string EntradasDepois = """[{"id":"d1","texto":"Chegamos à vila.","createdAt":"2026-10-01T12:00:00Z","imageUrls":[]},{"id":"d2","texto":"Encontramos o mapa.","createdAt":"2026-10-02T12:00:00Z","imageUrls":[]}]""";

    private FichaEstado Estado()
    {
        var http = FakeHttpMessageHandler.CreateClient(req =>
        {
            var caminho = req.RequestUri!.PathAndQuery.Replace("/api/", "");
            var corpo = req.Content is null ? "" : " " + req.Content.ReadAsStringAsync().Result;
            lock (_requisicoes)
                _requisicoes.Add($"{req.Method} {caminho}{corpo}");
            var resposta = _respostas.TryGetValue($"{req.Method} {caminho}", out var r) ? r
                : caminho.EndsWith("/diary") ? (HttpStatusCode.OK, Entradas)
                : (HttpStatusCode.OK, "{}");
            return new HttpResponseMessage(resposta.Item1) { Content = new StringContent(resposta.Item2, Encoding.UTF8, "application/json") };
        });
        return new FichaEstado(http, TipoDeFichaCliente.Personagem, "s1")
        {
            MinhasImagens = new List<ImageSummaryResponse> { new("img1", "/img/um.png", DateTime.UnixEpoch), new("img2", "/img/dois.png", DateTime.UnixEpoch) },
        };
    }

    private static int Avisos(FichaEstado estado, Func<Task> acao)
    {
        var avisos = 0;
        estado.Alterado += () => avisos++;
        acao().GetAwaiter().GetResult();
        return avisos;
    }

    [Fact]
    public async Task Carregar_diario_reads_the_sheet_diary()
    {
        var estado = Estado();

        await estado.CarregarDiarioAsync();

        _requisicoes.Should().Equal("GET character-sheets/s1/diary");
        estado.EntradasDoDiario.Select(e => e.Texto).Should().Equal("Chegamos à vila.");
    }

    [Fact]
    public void Adding_an_entry_posts_the_draft_then_reloads_clears_the_draft_and_notifies_once()
    {
        var estado = Estado();
        _respostas["GET character-sheets/s1/diary"] = (HttpStatusCode.OK, EntradasDepois);
        estado.FormularioDoDiario.Texto = "Encontramos o mapa.";
        estado.ImagensDaNovaEntrada = new List<string> { "img1" };

        Avisos(estado, estado.AdicionarEntradaAsync).Should().Be(1);

        _requisicoes.Should().Equal(
            "POST character-sheets/s1/diary {\"texto\":\"Encontramos o mapa.\",\"imageIds\":[\"img1\"]}", "GET character-sheets/s1/diary");
        estado.FormularioDoDiario.Texto.Should().BeEmpty();
        estado.ImagensDaNovaEntrada.Should().BeEmpty();
        estado.EntradasDoDiario.Select(e => e.Texto).Should().Equal("Chegamos à vila.", "Encontramos o mapa.");
    }

    [Fact]
    public void A_refused_entry_keeps_the_draft_shows_the_error_and_does_not_reload()
    {
        var estado = Estado();
        _respostas["POST character-sheets/s1/diary"] = (HttpStatusCode.BadRequest, """{"error":"x"}""");
        estado.FormularioDoDiario.Texto = "Encontramos o mapa.";

        Avisos(estado, estado.AdicionarEntradaAsync).Should().Be(1);

        _requisicoes.Should().ContainSingle().Which.Should().StartWith("POST ");
        estado.Erro.Should().Be("Não foi possível adicionar a entrada.");
        estado.FormularioDoDiario.Texto.Should().Be("Encontramos o mapa.");
    }

    // A edição começa com o texto e as imagens da entrada que estão na galeria (por URL); as outras ficam de fora.
    [Fact]
    public async Task Starting_an_edit_takes_the_text_and_the_gallery_images_of_the_entry()
    {
        var estado = Estado();
        await estado.CarregarDiarioAsync();
        _requisicoes.Clear();

        Avisos(estado, () => { estado.ComecarEdicao(estado.EntradasDoDiario[0]); return Task.CompletedTask; }).Should().Be(1);

        estado.EntradaEmEdicao.Should().Be("d1");
        estado.TextoEmEdicao.Should().Be("Chegamos à vila.");
        estado.ImagensEmEdicao.Should().Equal("img2");
        _requisicoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Cancelling_an_edit_drops_it_without_requests()
    {
        var estado = Estado();
        await estado.CarregarDiarioAsync();
        estado.ComecarEdicao(estado.EntradasDoDiario[0]);
        estado.TextoEmEdicao = "Outra coisa.";
        _requisicoes.Clear();

        Avisos(estado, () => { estado.CancelarEdicao(); return Task.CompletedTask; }).Should().Be(1);

        estado.EntradaEmEdicao.Should().BeNull();
        estado.TextoEmEdicao.Should().BeEmpty();
        estado.ImagensEmEdicao.Should().BeEmpty();
        _requisicoes.Should().BeEmpty();
    }

    [Fact]
    public async Task Saving_an_edit_puts_the_entry_then_reloads_ends_the_edit_and_notifies_once()
    {
        var estado = Estado();
        await estado.CarregarDiarioAsync();
        estado.ComecarEdicao(estado.EntradasDoDiario[0]);
        estado.TextoEmEdicao = "Chegamos ao anoitecer.";
        _requisicoes.Clear();
        _respostas["GET character-sheets/s1/diary"] = (HttpStatusCode.OK, EntradasDepois);

        Avisos(estado, () => estado.SalvarEntradaAsync("d1")).Should().Be(1);

        _requisicoes.Should().Equal(
            "PUT character-sheets/s1/diary/d1 {\"texto\":\"Chegamos ao anoitecer.\",\"imageIds\":[\"img2\"]}", "GET character-sheets/s1/diary");
        estado.EntradaEmEdicao.Should().BeNull();
        estado.TextoEmEdicao.Should().BeEmpty();
        estado.ImagensEmEdicao.Should().BeEmpty();
        estado.EntradasDoDiario.Should().HaveCount(2);
    }

    [Fact]
    public async Task A_refused_edit_stays_open_shows_the_error_and_does_not_reload()
    {
        var estado = Estado();
        await estado.CarregarDiarioAsync();
        estado.ComecarEdicao(estado.EntradasDoDiario[0]);
        _requisicoes.Clear();
        _respostas["PUT character-sheets/s1/diary/d1"] = (HttpStatusCode.BadRequest, """{"error":"x"}""");

        Avisos(estado, () => estado.SalvarEntradaAsync("d1")).Should().Be(1);

        _requisicoes.Should().ContainSingle().Which.Should().StartWith("PUT ");
        estado.Erro.Should().Be("Não foi possível salvar a entrada.");
        estado.EntradaEmEdicao.Should().Be("d1");
        estado.TextoEmEdicao.Should().Be("Chegamos à vila.");
    }

    // Como hoje, o DELETE não olha a resposta: relê o Diário de qualquer jeito.
    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public void Deleting_an_entry_deletes_then_reloads_and_notifies_once(HttpStatusCode status)
    {
        var estado = Estado();
        _respostas["DELETE character-sheets/s1/diary/d1"] = (status, "");
        _respostas["GET character-sheets/s1/diary"] = (HttpStatusCode.OK, "[]");

        Avisos(estado, () => estado.ExcluirEntradaAsync("d1")).Should().Be(1);

        _requisicoes.Should().Equal("DELETE character-sheets/s1/diary/d1", "GET character-sheets/s1/diary");
        estado.EntradasDoDiario.Should().BeEmpty();
    }

    [Fact]
    public void An_image_error_goes_to_the_page_alert_and_notifies_once()
    {
        var estado = Estado();

        Avisos(estado, () => { estado.MostrarErroDoDiario("Imagem grande demais."); return Task.CompletedTask; }).Should().Be(1);

        estado.Erro.Should().Be("Imagem grande demais.");
    }
}
