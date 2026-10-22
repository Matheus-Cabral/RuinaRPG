using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using RuinaRPG.Client.Shared;
using RuinaRPG.Client.Shared.Ficha;
using RuinaRPG.Tests.Client.Pages.Ficha.RedeDeSeguranca;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Ficha;

/// <summary>
/// A aba Posses compartilhada (E, Task 8) dentro das três páginas: uma por página, com o estado da ficha; o Inventário
/// (com o Equipamento Inicial e os Ciclos, que vêm do formulário da página) só onde o tipo tem Inventário; os Espólios
/// só onde o tipo tem Espólios; a edição das Afeições na linha só onde o tipo a tem; e os seletores de item do Catálogo
/// de cada tipo. O que se vê e as requisições ficam com a rede de segurança (RedePossesTests).
/// </summary>
public class AbaPossesTests : RedeDeSegurancaBase
{
    public static TheoryData<string> Tipos => new() { "Personagem", "NPC", "Criatura" };

    private async Task<IRenderedComponent<IComponent>> AbrirAsync(string tipo, string papel = "GM")
    {
        var cut = await RenderFichaAsync(tipo, papel);
        await IrParaAbaAsync(cut, "Posses");
        await EsperarCalmariaAsync();
        return cut;
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_tab_is_the_shared_component_with_the_page_state(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<AbaPosses>().Should().ContainSingle();
        cut.FindComponent<AbaPosses>().Instance.Estado.Should().BeSameAs(cut.FindComponent<CascadingValue<FichaEstado>>().Instance.Value);
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Inventario_or_espolios_by_the_kind(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<InventarioSection>().Should().HaveCount(tipo == "Criatura" ? 0 : 1);
        cut.FindComponents<EspoliosSection>().Should().HaveCount(tipo == "Criatura" ? 1 : 0);
    }

    // O Equipamento Inicial e os Ciclos vivem no formulário auto-salvo da página, que o passa (com os retornos que
    // salvam e releem) à aba e dela ao Inventário.
    [Theory]
    [InlineData("Personagem", 4)]
    [InlineData("NPC", 2)]
    public async Task The_inventory_gets_ciclos_and_the_kit_from_the_page_form(string tipo, int ciclos)
    {
        var cut = await AbrirAsync(tipo);

        var aba = cut.FindComponent<AbaPosses>().Instance;
        var inventario = cut.FindComponent<InventarioSection>().Instance;
        inventario.Formulario.Should().BeSameAs(aba.Formulario).And.NotBeNull();
        inventario.Formulario.Tipo.Nome.Should().Be(tipo);
        inventario.Formulario.Ciclos.Should().Be(ciclos);
        inventario.Formulario.EquipmentKitId.Should().BeNull();
        inventario.SalvarAgora.HasDelegate.Should().BeTrue();
        inventario.EquipagemEscolhida.HasDelegate.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Afeicoes_editable_in_the_row_only_where_the_kind_edits_them(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        cut.FindComponents<AfeicoesNaLinha>().Should().HaveCount(tipo == "Personagem" ? 1 : 0);
    }

    // Os seletores de item: o do Catálogo com "+" onde o tipo cadastra item no seletor, o simples no Personagem.
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task The_item_pickers_follow_the_kind(string tipo)
    {
        var cut = await AbrirAsync(tipo);

        var seletores = cut.FindComponents<SeletorDeItem>().Select(s => s.Instance.Tipo);
        seletores.Should().Equal(tipo == "Criatura" ? new[] { "Artefato" } : new[] { "ItemGeral", "Artefato" });
        cut.FindComponents<CatalogoItemPicker>().Should().HaveCount(tipo == "Personagem" ? 0 : 2);
        cut.FindComponents<EntityPicker>().Should().HaveCount(2);
    }

    // Uma filha sem parâmetros (AfeicoesNaLinha, EspoliosSection) não re-renderiza só porque a aba re-renderizou: depois de
    // adicionar, a lista nova tem de aparecer em todo tipo (a rede de segurança devolve a mesma linha e não vê isto).
    [Theory]
    [MemberData(nameof(Tipos))]
    public async Task Adding_an_afeicao_shows_the_reloaded_list(string tipo)
    {
        var cut = await AbrirAsync(tipo);
        var form = Botao(AbaAberta(cut), "Adicionar Afeição").Closest("form")!;
        await cut.InvokeAsync(() => Campo(form, "Nome").Change("Ferreira"));
        LimparRequisicoes();
        Responder("/affections", new[]
        {
            new { id = "afe1", nome = "Taverneiro", favorabilidade = 2 },
            new { id = "afe2", nome = "Ferreira", favorabilidade = 0 },
        });

        await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Adicionar Afeição").Closest("form")!.Submit());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain(r => r.StartsWith("GET ") && r.EndsWith("/affections")));
        cut.WaitForAssertion(() => Botao(AbaAberta(cut), "Adicionar Afeição").Closest("form")!.ParentElement!
            .QuerySelectorAll("table tbody tr").Should().HaveCount(2));
    }

    [Fact]
    public async Task Adding_an_espolio_shows_the_reloaded_list()
    {
        var cut = await AbrirAsync("Criatura");
        LimparRequisicoes();
        Responder("/spoils", new[]
        {
            new { id = "sp1", itemId = "i6", nome = "Presa de Lobo", custo = 5, qtd = 2, custoTotal = 10, dt = 12, descricao = "Afiada." },
            new { id = "sp2", itemId = "i7", nome = "Couro", custo = 3, qtd = 1, custoTotal = 3, dt = 10, descricao = "Curtido." },
        });

        await cut.InvokeAsync(() => Botao(AbaAberta(cut), "Adicionar Espólio").Closest("form")!.Submit());

        await EsperarRequisicoesAsync(cut, todas => todas.Should().Contain(r => r.StartsWith("GET ") && r.EndsWith("/spoils")));
        cut.WaitForAssertion(() => Botao(AbaAberta(cut), "Adicionar Espólio").Closest("form")!.ParentElement!
            .QuerySelectorAll("table tbody tr").Should().HaveCount(2));
    }

    // Um tipo com Inventário sem o formulário da página falha com uma mensagem clara (não um NullReferenceException).
    [Fact]
    public void A_kind_with_inventory_without_the_page_form_fails_clearly()
    {
        var estado = new FichaEstado(new HttpClient(), TipoDeFichaCliente.Personagem, SheetId);

        var renderizar = () => Render<AbaPosses>(p => p.AddCascadingValue(estado));

        renderizar.Should().Throw<InvalidOperationException>().WithMessage("*Inventário*Formulario*");
    }
}
