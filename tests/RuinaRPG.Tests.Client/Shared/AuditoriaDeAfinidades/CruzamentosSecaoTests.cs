using Bunit;
using System.Net;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.AuditoriaDeAfinidades;
using RuinaRPG.Contracts.Rules;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.AuditoriaDeAfinidades;

public class CruzamentosSecaoTests : SecoesDeAfinidadesTestes
{
    private static readonly Guid Gelo = Guid.Parse("00000000-0000-0000-0000-0000000000a1");

    [Fact]
    public async Task Lista_cada_cruzamento_com_os_nomes_do_catalogo_e_os_minimos()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        cut.FindAll("button[aria-label='Remover cruzamento de Água e Ar']").Should().HaveCount(1);
        (Coluna(cut, 1).Single(), Coluna(cut, 2).Single(), Coluna(cut, 3).Single(), Coluna(cut, 4).Single(), Coluna(cut, 5).Single())
            .Should().Be(("Gelo", "Água", "1", "Ar", "2"));
    }

    [Fact]
    public async Task Editar_o_minimo_de_um_lado_grava_o_cruzamento_inteiro()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        await DefinirAsync(cut, cut.FindComponents<MudNumericField<int>>().Where(f => f.Instance.Label is null).ElementAt(1).Instance.ValueChanged, 4);

        var put = Api.Escritas.Should().ContainSingle().Subject;
        (put.Metodo, put.Caminho).Should().Be(("PUT", $"afinidades/cruzamentos/{Gelo}"));
        put.Ler<SalvarCruzamentoDeAfinidadeRequest>().Should().Be(new SalvarCruzamentoDeAfinidadeRequest(1, 1, 3, 4, 4));
    }

    [Fact]
    public async Task O_Sub_Elemento_so_lista_os_Sub_Elementos()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        Opcoes<int?>(cut, "Sub-Elemento").Should().Equal("Gelo", "Raio");
    }

    [Fact]
    public async Task Adicionar_cruzamento_grava_os_dois_elementos_os_minimos_e_o_Sub_Elemento()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        MudSelect<int?> Select(string label) => cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == label).Instance;
        MudNumericField<int> Minimo(string label) => cut.FindComponents<MudNumericField<int>>().Single(s => s.Instance.Label == label).Instance;
        await DefinirAsync(cut, Select("Elemento").ValueChanged, (int?)9);
        await DefinirAsync(cut, Minimo("Mínimo").ValueChanged, 2);
        await DefinirAsync(cut, Select("Outro elemento").ValueChanged, (int?)3);
        await DefinirAsync(cut, Minimo("Mínimo do outro").ValueChanged, 1);
        await DefinirAsync(cut, Select("Sub-Elemento").ValueChanged, (int?)7);
        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar cruzamento")).Click();
        await Task.Delay(50);

        var post = Api.Escritas.Should().ContainSingle().Subject;
        (post.Metodo, post.Caminho).Should().Be(("POST", "afinidades/cruzamentos"));
        post.Ler<SalvarCruzamentoDeAfinidadeRequest>().Should().Be(new SalvarCruzamentoDeAfinidadeRequest(9, 2, 3, 1, 7));
        cut.FindAll("button[aria-label='Remover cruzamento de Alma e Ar']").Should().HaveCount(1);
    }

    [Fact]
    public async Task Uma_recusa_mostra_a_mensagem_do_servidor()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();
        Api.Recusa = (HttpStatusCode.BadRequest, "Já existe um cruzamento de Água e Ar.");
        await EscolherNovoAsync(cut, 3, 1, 4);

        cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar cruzamento")).Click();
        await Task.Delay(50);

        cut.Markup.Should().Contain("Já existe um cruzamento de Água e Ar.");
    }

    [Fact]
    public async Task Remover_pede_confirmacao_e_remove()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        await ClicarAsync(cut, "button[aria-label='Remover cruzamento de Água e Ar']");
        Api.Escritas.Should().BeEmpty();
        await ConfirmarAsync(cut, "Remover");

        Api.Escritas.Should().ContainSingle().Which.Should().Be(new ApiFalsaDeAfinidades.Pedido("DELETE", $"afinidades/cruzamentos/{Gelo}", null));
        cut.FindAll("button[aria-label='Remover cruzamento de Água e Ar']").Should().BeEmpty();
    }

    private static async Task EscolherNovoAsync(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> cut, int? a, int? b, int? sub)
    {
        MudSelect<int?> Select(string label) => cut.FindComponents<MudSelect<int?>>().Single(s => s.Instance.Label == label).Instance;
        await DefinirAsync(cut, Select("Elemento").ValueChanged, a);
        await DefinirAsync(cut, Select("Outro elemento").ValueChanged, b);
        await DefinirAsync(cut, Select("Sub-Elemento").ValueChanged, sub);
    }

    private static bool AdicionarHabilitado(Bunit.IRenderedComponent<Bunit.Rendering.ContainerFragment> cut) =>
        !cut.FindAll("button").First(b => b.TextContent.Contains("Adicionar cruzamento")).HasAttribute("disabled");

    [Fact]
    public async Task O_cruzamento_novo_comeca_sem_nada_escolhido_e_so_habilita_Adicionar_com_os_tres()
    {
        var cut = await RenderizarAsync<CruzamentosSecao>();

        cut.FindComponents<MudSelect<int?>>().Where(s => s.Instance.Label is not null)
            .Select(s => s.Find("input").GetAttribute("value") ?? "").Should().Equal("", "", "");
        AdicionarHabilitado(cut).Should().BeFalse();

        await EscolherNovoAsync(cut, 9, 3, null);
        AdicionarHabilitado(cut).Should().BeFalse();

        await EscolherNovoAsync(cut, 9, 3, 7);
        AdicionarHabilitado(cut).Should().BeTrue();
    }
}
