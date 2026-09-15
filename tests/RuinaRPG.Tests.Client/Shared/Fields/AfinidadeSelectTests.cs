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
}
