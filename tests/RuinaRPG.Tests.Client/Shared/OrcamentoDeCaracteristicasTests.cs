using Bunit;
using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

/// <summary>
/// Ficha de Personagem 5.d: Positivas e Negativas têm cada uma o seu limite — o de Negativas é o
/// dobro —, e a ficha mostra o gasto de cada lista contra o limite dela.
/// </summary>
public class OrcamentoDeCaracteristicasTests : MudBunitContext
{
    private IRenderedComponent<OrcamentoDeCaracteristicas> RenderOrcamento(int totalPositivas, int totalNegativas) =>
        Render<OrcamentoDeCaracteristicas>(p => p
            .Add(x => x.TotalPositivas, totalPositivas)
            .Add(x => x.TotalNegativas, totalNegativas)
            .Add(x => x.LimitePositivas, 5)
            .Add(x => x.LimiteNegativas, 10));

    [Fact]
    public void Each_list_is_shown_against_its_own_limit_with_the_negative_total_as_a_magnitude()
    {
        var cut = RenderOrcamento(totalPositivas: 3, totalNegativas: -7);

        cut.Find(".orcamento-positivas").TextContent.Trim().Should().Be("Positivas: 3 / 5");
        cut.Find(".orcamento-negativas").TextContent.Trim().Should().Be("Negativas: 7 / 10");
    }

    [Fact]
    public void Negativas_between_the_positive_and_the_negative_limit_are_not_flagged_as_over_budget()
    {
        var cut = RenderOrcamento(totalPositivas: 5, totalNegativas: -10);

        cut.FindAll(".mud-error-text").Should().BeEmpty();
    }

    [Fact]
    public void A_list_past_its_own_limit_is_flagged()
    {
        var cut = RenderOrcamento(totalPositivas: 6, totalNegativas: -11);

        cut.Find(".orcamento-positivas").ClassList.Should().Contain("mud-error-text");
        cut.Find(".orcamento-negativas").ClassList.Should().Contain("mud-error-text");
    }
}
