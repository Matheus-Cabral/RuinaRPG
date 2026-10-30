using Bunit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using RuinaRPG.Contracts.Rules;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

/// <summary>O campo Afinidade lê o catálogo de elementos do Auditor (GET afinidades/catalogo); o valor é o id.</summary>
public class AfinidadeSelectTests : MudBunitContext
{
    private static List<int?> Valores(IRenderedComponent<AfinidadeSelect> cut) =>
        cut.FindComponents<MudSelectItem<int?>>().Select(i => i.Instance.Value).ToList();

    [Fact]
    public void Lists_the_16_catalog_elements_that_are_not_Caminhos()
    {
        var cut = Render<AfinidadeSelect>();

        cut.WaitForAssertion(() => Valores(cut).Should().HaveCount(16));
        Valores(cut).Should().NotContain([9, 14, 18]);
    }

    [Fact]
    public void Elemento_criado_aparece_nos_pickers()
    {
        Services.AddSingleton(ElementosDeTeste.Cliente([.. ElementosDeTeste.Semeados, new ElementoDeAfinidadeResponse(19, "Luz", "SubElemento", null)]));

        var root = RenderWithPopover<AfinidadeSelect>();

        root.WaitForAssertion(() => root.FindComponents<MudSelectItem<int?>>().Should().HaveCount(17));
        root.FindComponent<MudSelect<int?>>().Find(".mud-input-control").MouseDown();
        root.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).Should().Contain(["Água", "Invocação", "Luz"]);
    }

    [Fact]
    public void A_saved_Caminho_is_still_listed_with_its_catalog_name_so_the_select_is_not_blank()
    {
        var root = RenderWithPopover<AfinidadeSelect>(("Value", (int?)14));

        root.WaitForAssertion(() => root.FindComponents<MudSelectItem<int?>>().Select(i => i.Instance.Value).Should().Contain(14));
        root.FindComponent<MudSelect<int?>>().Find(".mud-input-control").MouseDown();
        root.FindAll(".mud-list-item").Select(li => li.TextContent.Trim()).Should().Contain("Vida");
    }

    [Fact]
    public void Without_a_saved_Caminho_no_Caminho_appears()
    {
        var cut = Render<AfinidadeSelect>(p => p.Add(x => x.Value, 2));

        cut.WaitForAssertion(() => Valores(cut).Should().HaveCount(16));
        Valores(cut).Should().NotContain([9, 14, 18]);
    }
}
