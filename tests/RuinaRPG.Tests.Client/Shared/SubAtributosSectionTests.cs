using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Ficha de Personagem 2.b: a seção Sub-Atributos, igual nas fichas de Personagem, NPC e Criatura.
/// O que é relacionado fica junto, cada grupo no seu bloco — Defesa Natural com a Cobertura, e a
/// Afinidade com a Eficiência Elemental e o Dano Elemental que saem dela.
/// </summary>
public class SubAtributosSectionTests : MudBunitContext
{
    private static readonly SubAttributesResponse Valores = new(
        Iniciativa: 4, Movimentacao: 6, EsquivaNatural: 12, DefesaNatural: 15, ReducaoFisica: 2, ReducaoMagica: 1,
        PesoAtual: null, PesoMaximo: null, EficienciaElemental: 3, DanoElemental: 2);

    private IRenderedComponent<SubAtributosSection> RenderSecao(bool comAfinidade = true, string? afinidade = "Fogo",
        Action<string?>? afinidadeChanged = null, Action<string>? coberturaChanged = null) =>
        Render<SubAtributosSection>(p => p
            .Add(x => x.SubAtributos, Valores)
            .Add(x => x.Cobertura, "Parcial")
            .Add(x => x.CoberturaChanged, v => coberturaChanged?.Invoke(v))
            .Add(x => x.ComAfinidade, comAfinidade)
            .Add(x => x.Afinidade, afinidade)
            .Add(x => x.AfinidadeChanged, v => afinidadeChanged?.Invoke(v)));

    private static List<string> Textos(IRenderedComponent<SubAtributosSection> cut, string seletor) =>
        cut.FindAll(seletor).Select(e => e.TextContent.Trim()).ToList();

    [Fact]
    public void The_sub_attributes_that_stand_alone_are_listed_first()
    {
        var cut = RenderSecao();

        Textos(cut, ".subatributos-gerais .subatributo").Should().Equal(
            "Iniciativa: 4", "Movimentação: 6", "Esquiva Natural: 12", "Redução Física: 2", "Redução Mágica: 1");
    }

    // Defesa Natural e a Cobertura que entra na sua conta ficam juntas, numa coluna à direita dos
    // demais sub-atributos — sem moldura, uma coisa por linha.
    [Fact]
    public void Defesa_Natural_and_the_Cobertura_that_feeds_it_are_a_column_beside_the_other_sub_attributes()
    {
        var cut = RenderSecao();

        var topo = cut.Find(".subatributos-topo");
        topo.Children.Select(c => c.ClassName).Should().Equal("subatributos-gerais", "subatributos-defesa");
        var defesa = topo.Children[1];
        defesa.ClassList.Should().NotContain("mud-paper");
        defesa.Children[0].TextContent.Trim().Should().Be("Defesa Natural: 15");
        defesa.Children[1].ClassList.Should().Contain("cobertura-rating");
        cut.FindComponent<CoberturaRating>().Instance.Value.Should().Be("Parcial");
    }

    [Fact]
    public void The_Afinidade_block_comes_last_after_the_two_columns()
    {
        var cut = RenderSecao();

        var topo = cut.Find(".subatributos-topo");
        var seguinte = topo.NextElementSibling!;
        seguinte.ClassList.Should().Contain("subatributos-bloco-afinidade");
        seguinte.NextElementSibling.Should().BeNull();
    }

    [Fact]
    public void The_Afinidade_and_the_two_values_derived_from_it_share_a_block_that_says_how_they_are_derived()
    {
        var cut = RenderSecao(afinidade: "Agua");

        var bloco = cut.Find(".subatributos-bloco-afinidade");
        bloco.QuerySelector(".subatributos-bloco-titulo")!.TextContent.Trim().Should().Be("Afinidade Elemental");
        cut.FindComponent<AfinidadeSelect>().Instance.Value.Should().Be("Agua");
        bloco.QuerySelector(".subatributo-eficiencia .subatributo-valor")!.TextContent.Trim().Should().Be("3");
        bloco.QuerySelector(".subatributo-eficiencia .subatributo-regra")!.TextContent.Trim().Should().Be("conforme a Tabela de Afinidades");
        bloco.QuerySelector(".subatributo-dano .subatributo-valor")!.TextContent.Trim().Should().Be("2");
        bloco.QuerySelector(".subatributo-dano .subatributo-regra")!.TextContent.Trim().Should().Be("conforme a Tabela de Afinidades");
    }

    [Fact]
    public void Without_a_chosen_Afinidade_the_captions_still_point_to_the_Tabela_de_Afinidades()
    {
        var cut = RenderSecao(afinidade: null);

        cut.Find(".subatributo-eficiencia .subatributo-regra").TextContent.Trim().Should().Be("conforme a Tabela de Afinidades");
        cut.Find(".subatributo-dano .subatributo-regra").TextContent.Trim().Should().Be("conforme a Tabela de Afinidades");
    }

    [Fact]
    public void A_sheet_without_elemental_values_has_no_Afinidade_block()
    {
        var cut = RenderSecao(comAfinidade: false);

        cut.FindAll(".subatributos-bloco-afinidade").Should().BeEmpty();
        cut.FindComponents<AfinidadeSelect>().Should().BeEmpty();
        cut.FindAll(".subatributos-defesa").Should().ContainSingle();
    }

    [Fact]
    public async Task Choosing_an_Afinidade_notifies_the_sheet()
    {
        string? escolhida = null;
        var cut = RenderSecao(afinidadeChanged: v => escolhida = v);

        await cut.InvokeAsync(() => cut.FindComponent<AfinidadeSelect>().Instance.ValueChanged.InvokeAsync("Gelo"));

        escolhida.Should().Be("Gelo");
    }

    [Fact]
    public async Task Changing_the_Cobertura_notifies_the_sheet()
    {
        string? nova = null;
        var cut = RenderSecao(coberturaChanged: v => nova = v);

        await cut.InvokeAsync(() => cut.FindComponent<CoberturaRating>().Instance.ValueChanged.InvokeAsync("Completa"));

        nova.Should().Be("Completa");
    }
}
