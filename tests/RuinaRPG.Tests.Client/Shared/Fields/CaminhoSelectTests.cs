using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class CaminhoSelectTests : MudBunitContext
{
    [Fact]
    public void Offers_exactly_Alma_Vida_and_Mundano()
    {
        var cut = Render<CaminhoSelect>();

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Equal("Alma", "Vida", "Mundano");
    }

    [Fact]
    public void A_legacy_free_text_value_is_still_listed_so_it_can_be_displayed()
    {
        var cut = Render<CaminhoSelect>(p => p.Add(x => x.Value, "Caminho da Fênix"));

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Contain("Caminho da Fênix");
    }

    [Fact]
    public async Task Changing_the_selection_raises_ValueChanged()
    {
        string? novo = null;
        var cut = Render<CaminhoSelect>(p => p.Add(x => x.ValueChanged, v => novo = v));

        await cut.InvokeAsync(() => cut.Instance.SetValueForTests("Vida"));

        novo.Should().Be("Vida");
    }
}
