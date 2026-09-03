using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class ElementoSubElementoFieldsTests : MudBunitContext
{
    [Fact]
    public void Elemento_and_SubElemento_selects_both_render_with_no_selection()
    {
        // Unlike a cascading picker, Sub-Elemento must not depend on an Elemento being chosen
        // first — otherwise the cell is empty until then, which reads as a missing field in a
        // table row (the complaint this component was rewritten to fix).
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, (string?)null)
            .Add(x => x.SubElemento, (string?)null));

        cut.FindComponents<MudSelect<string>>().Should().HaveCount(2);
    }

    [Fact]
    public async Task Setting_Elemento_does_not_change_or_clear_SubElemento()
    {
        string? newSubElemento = "unchanged";
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, "Ar")
            .Add(x => x.SubElemento, "Vida")
            .Add(x => x.SubElementoChanged, v => newSubElemento = v));

        await cut.InvokeAsync(() => cut.Instance.SetElementoForTests("Terra"));

        newSubElemento.Should().Be("unchanged");
    }

    [Fact]
    public async Task Setting_SubElemento_raises_SubElementoChanged_independently_of_Elemento()
    {
        string? newSubElemento = null;
        var cut = Render<ElementoSubElementoFields>(p => p
            .Add(x => x.Elemento, (string?)null)
            .Add(x => x.SubElemento, (string?)null)
            .Add(x => x.SubElementoChanged, v => newSubElemento = v));

        await cut.InvokeAsync(() => cut.Instance.SetSubElementoForTests("Invocacao"));

        newSubElemento.Should().Be("Invocacao");
    }
}
