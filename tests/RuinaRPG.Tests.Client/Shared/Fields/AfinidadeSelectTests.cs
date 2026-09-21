using Bunit;
using FluentAssertions;
using MudBlazor;
using RuinaRPG.Client.Shared.Fields;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared.Fields;

public class AfinidadeSelectTests : MudBunitContext
{
    [Fact]
    public void Without_OpcoesPermitidas_shows_every_one_of_the_18_values()
    {
        var cut = Render<AfinidadeSelect>();

        cut.FindComponents<MudSelectItem<string>>().Should().HaveCount(AfinidadeSelect.Afinidades.Length);
    }

    [Fact]
    public void With_OpcoesPermitidas_shows_only_the_given_subset()
    {
        var subset = new[] { ("Fogo", "Fogo"), ("Necromancia", "Necromancia") };

        var cut = Render<AfinidadeSelect>(p => p.Add(x => x.OpcoesPermitidas, subset));

        cut.FindComponents<MudSelectItem<string>>().Should().HaveCount(2);
    }

    [Fact]
    public void Afinidades_has_16_values_without_Alma_and_Vida_which_are_Caminhos()
    {
        AfinidadeSelect.Afinidades.Should().HaveCount(16);
        AfinidadeSelect.Afinidades.Select(a => a.Valor).Should().NotContain(["Alma", "Vida"]);
    }

    [Fact]
    public void A_legacy_saved_Alma_or_Vida_is_still_listed_so_the_select_is_not_blank()
    {
        var cut = Render<AfinidadeSelect>(p => p.Add(x => x.Value, "Vida"));

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().Contain("Vida");
    }

    [Fact]
    public void Without_a_legacy_value_no_Caminho_appears()
    {
        var cut = Render<AfinidadeSelect>(p => p.Add(x => x.Value, "Fogo"));

        cut.FindComponents<MudSelectItem<string>>().Select(i => i.Instance.Value).Should().NotContain(["Alma", "Vida"]);
    }
}
