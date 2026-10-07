using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Catálogo de Itens e Equipamentos R0003/R0004: a Subcategoria é um "dropdown extensível" — lista
/// as subcategorias já existentes para o Tipo e aceita um texto novo.
/// </summary>
public class SubcategoriaFieldTests : MudBunitContext
{
    private static Func<string, Task<List<string>>> Existentes(params (string Tipo, string[] Valores)[] porTipo) =>
        tipo => Task.FromResult(porTipo.Where(p => p.Tipo == tipo).SelectMany(p => p.Valores).ToList());

    private static async Task<List<string>> BuscarAsync(IRenderedComponent<SubcategoriaField> cut, string texto)
    {
        var opcoes = await cut.FindComponent<MudAutocomplete<string>>().Instance.SearchFunc!(texto, CancellationToken.None)!;
        return opcoes.OfType<string>().ToList();
    }

    [Fact]
    public async Task Lists_the_existing_subcategorias_of_its_Tipo_sorted_and_without_blanks_or_duplicates()
    {
        var cut = Render<SubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Existentes, Existentes(("Arma", ["Machados", "Espadas", "", "Espadas"]), ("ItemGeral", ["Ferramentas"]))));

        (await BuscarAsync(cut, "")).Should().Equal("Espadas", "Machados");
    }

    [Fact]
    public async Task Typing_narrows_the_list_ignoring_case()
    {
        var cut = Render<SubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Existentes, Existentes(("Arma", ["Machados", "Espadas"]))));

        (await BuscarAsync(cut, "ESP")).Should().Equal("Espadas");
    }

    [Fact]
    public void A_text_that_is_not_in_the_list_becomes_the_value()
    {
        string? emitido = null;
        var cut = Render<SubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Existentes, Existentes(("Arma", ["Espadas"])))
            .Add(x => x.ValueChanged, v => emitido = v));

        cut.Find("input").Input("Foices");
        cut.Find("input").Blur();

        cut.WaitForAssertion(() => emitido.Should().Be("Foices"));
    }

    [Fact]
    public async Task Changing_the_Tipo_lists_the_subcategorias_of_the_new_Tipo()
    {
        var existentes = Existentes(("Arma", ["Espadas"]), ("Escudo", ["Broquéis"]));
        var cut = Render<SubcategoriaField>(p => p.Add(x => x.Tipo, "Arma").Add(x => x.Existentes, existentes));
        await BuscarAsync(cut, "");

        cut.Render(p => p.Add(x => x.Tipo, "Escudo").Add(x => x.Existentes, existentes));

        (await BuscarAsync(cut, "")).Should().Equal("Broquéis");
    }

    [Fact]
    public async Task The_existing_subcategorias_are_loaded_once_per_Tipo_not_once_per_keystroke()
    {
        var chamadas = 0;
        var cut = Render<SubcategoriaField>(p => p
            .Add(x => x.Tipo, "Arma")
            .Add(x => x.Existentes, _ => { chamadas++; return Task.FromResult(new List<string> { "Espadas" }); }));

        await BuscarAsync(cut, "");
        await BuscarAsync(cut, "e");
        await BuscarAsync(cut, "es");

        chamadas.Should().Be(1);
    }

    [Fact]
    public async Task Without_a_source_of_existing_subcategorias_it_lists_nothing()
    {
        var cut = Render<SubcategoriaField>(p => p.Add(x => x.Tipo, "Arma"));

        (await BuscarAsync(cut, "")).Should().BeEmpty();
    }
}
