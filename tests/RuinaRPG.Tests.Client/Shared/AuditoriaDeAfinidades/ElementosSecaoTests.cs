using Bunit;
using System.Net;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.AuditoriaDeAfinidades;
using RuinaRPG.Contracts.Rules;
using RuinaRPG.Domain.Afinidades;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

public class ElementosSecaoTests : SecoesDeAfinidadesTestes
{
    // Os campos sem rótulo são os das linhas; os com rótulo, os do formulário de elemento novo.
    private static List<MudTextField<string>> NomesDasLinhas(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> cut) =>
        cut.FindComponents<MudTextField<string>>().Where(f => f.Instance.Label is null).Select(f => f.Instance).ToList();

    [Fact]
    public async Task Lista_os_elementos_do_catalogo_com_tipo_e_teto()
    {
        var cut = await RenderizarAsync<ElementosSecao>();

        Coluna(cut, 1).Should().Equal("Água", "Ar", "Gelo", "Raio", "Alma");
        Coluna(cut, 2).Should().Equal("Elemento base", "Elemento base", "Sub-Elemento", "Sub-Elemento", "Caminho");
        Coluna(cut, 3).Select(v => v ?? "").Should().Equal("", "", "5", "", "");
    }

    [Fact]
    public async Task Adicionar_elemento_grava_nome_tipo_e_teto_e_recarrega_o_catalogo_da_sessao()
    {
        var cut = await RenderizarAsync<ElementosSecao>();
        var leituras = Api.LeiturasDoCatalogo;

        await DefinirAsync(cut, cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Nome").Instance.ValueChanged, "Luz");
        await DefinirAsync(cut, cut.FindComponents<MudSelect<TipoDeElemento>>().Single(f => f.Instance.Label == "Tipo").Instance.ValueChanged, TipoDeElemento.SubElemento);
        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int?>>().Single(f => f.Instance.Label == "Teto").Instance.ValueChanged, (int?)3);
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar elemento")).Click();
        await Task.Delay(50);

        var post = Api.Escritas.Should().ContainSingle().Subject;
        (post.Metodo, post.Caminho).Should().Be(("POST", "afinidades/elementos"));
        post.Ler<SalvarElementoDeAfinidadeRequest>().Should().Be(new SalvarElementoDeAfinidadeRequest("Luz", "SubElemento", 3));
        Api.LeiturasDoCatalogo.Should().BeGreaterThan(leituras);
        Coluna(cut, 1).Should().Contain("Luz");
        cut.FindComponents<MudTextField<string>>().Single(f => f.Instance.Label == "Nome").Find("input").GetAttribute("value").Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task Renomear_e_tirar_o_teto_de_uma_linha_grava_o_elemento_inteiro()
    {
        var cut = await RenderizarAsync<ElementosSecao>();

        await DefinirAsync(cut, NomesDasLinhas(cut)[2].ValueChanged, "Geada");
        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int?>>().Where(f => f.Instance.Label is null).ElementAt(2).Instance.ValueChanged, (int?)null);

        Api.Escritas.Select(e => (e.Metodo, e.Caminho)).Should().Equal(("PUT", "afinidades/elementos/4"), ("PUT", "afinidades/elementos/4"));
        Api.Escritas[0].Ler<SalvarElementoDeAfinidadeRequest>().Should().Be(new SalvarElementoDeAfinidadeRequest("Geada", "SubElemento", 5));
        Api.Escritas[1].Ler<SalvarElementoDeAfinidadeRequest>().Should().Be(new SalvarElementoDeAfinidadeRequest("Geada", "SubElemento", null));
        Coluna(cut, 1)[2].Should().Be("Geada");
    }

    [Fact]
    public async Task Remover_pede_confirmacao_e_remove()
    {
        var cut = await RenderizarAsync<ElementosSecao>();

        await ClicarAsync(cut, "button[aria-label='Remover Raio']");
        Api.Escritas.Should().BeEmpty();
        await ConfirmarAsync(cut, "Remover");

        Api.Escritas.Should().ContainSingle().Which.Should().Be(new ApiFalsaDeAfinidades.Pedido("DELETE", "afinidades/elementos/7", null));
        cut.FindAll("button[aria-label='Remover Raio']").Should().BeEmpty();
    }

    [Fact]
    public async Task Remover_um_elemento_usado_mostra_as_contagens_e_ele_continua()
    {
        var cut = await RenderizarAsync<ElementosSecao>();
        Api.Recusa = (HttpStatusCode.Conflict, "Este elemento é usado por 2 ficha(s) e 1 Passiva(s).");

        await ClicarAsync(cut, "button[aria-label='Remover Gelo']");
        await ConfirmarAsync(cut, "Remover");

        cut.Markup.Should().Contain("Este elemento é usado por 2 ficha(s) e 1 Passiva(s).");
        cut.FindAll("button[aria-label='Remover Gelo']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Cancelar_a_remocao_nao_chama_a_api()
    {
        var cut = await RenderizarAsync<ElementosSecao>();

        await ClicarAsync(cut, "button[aria-label='Remover Raio']");
        await ConfirmarAsync(cut, "Cancelar");

        Api.Escritas.Should().BeEmpty();
    }
}
