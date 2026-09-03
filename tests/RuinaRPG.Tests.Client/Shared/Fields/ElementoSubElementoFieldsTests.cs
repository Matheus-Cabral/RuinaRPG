using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class ElementoSubElementoFieldsTests : MudBunitContext
{
    [Theory]
    [InlineData("Ar", new[] { "Gelo", "Raio", "Prever", "Ecomancia", "Alma" })]
    [InlineData("Agua", new[] { "Gelo", "Flora", "Purificar", "Hemomancia", "Alma" })]
    [InlineData("Fogo", new[] { "Raio", "Ferro", "Curar", "Necromancia", "Vida" })]
    [InlineData("Terra", new[] { "Ferro", "Flora", "Aprimorar", "Invocacao", "Vida" })]
    public void SubElemento_options_are_scoped_to_the_selected_Elemento(string elemento, string[] expected)
    {
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, elemento)
            .Add(x => x.SubElemento, (string?)null));

        cut.Instance.SubElementoOptions().Select(o => o.Valor).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Changing_Elemento_clears_a_now_invalid_SubElemento()
    {
        string? newSubElemento = "not-cleared-yet";
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, "Ar")
            .Add(x => x.SubElemento, "Prever")
            .Add(x => x.SubElementoChanged, v => newSubElemento = v));

        await cut.InvokeAsync(() => cut.Instance.SetElementoForTests("Terra"));

        newSubElemento.Should().BeNull();
    }

    [Fact]
    public async Task Changing_Elemento_keeps_a_SubElemento_still_valid_in_the_new_Elemento()
    {
        // Gelo is valid under both Ar and Agua.
        string? newSubElemento = "not-cleared-yet";
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, "Ar")
            .Add(x => x.SubElemento, "Gelo")
            .Add(x => x.SubElementoChanged, v => newSubElemento = v));

        await cut.InvokeAsync(() => cut.Instance.SetElementoForTests("Agua"));

        newSubElemento.Should().Be("not-cleared-yet");
    }
}
