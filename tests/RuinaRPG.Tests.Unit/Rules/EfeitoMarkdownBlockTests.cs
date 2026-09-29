using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class EfeitoMarkdownBlockTests
{
    private static EfeitoParaLivro Efeito(TipoDeCusto tipo, int? fixo = null, int? porUnidade = null, string? unidade = null,
        string? derivado = null, int? max = null, bool maxPorGrau = false, int? alt = null, int? altGrau = null,
        IReadOnlyList<IReadOnlyList<string>>? pre = null) =>
        new("Chama Viva", 2, "Envolve o alvo em chamas.", tipo, fixo, porUnidade, unidade, derivado, max, maxPorGrau, alt, altGrau, pre ?? []);

    [Fact]
    public void Fixo() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.Fixo, fixo: 3)).Should().Be(
            "## Chama Viva\n\n**Gasto:** 3 PI.\n\nEnvolve o alvo em chamas.\n");

    [Fact]
    public void Por_unidade_with_max_per_grau_and_prerequisites()
    {
        var e = Efeito(TipoDeCusto.PorUnidade, porUnidade: 2, unidade: "Dado", max: 3, maxPorGrau: true,
            pre: [["Duração"], ["Dano", "Alcance"]]);
        EfeitoMarkdownBlock.Gerar(e).Should().Be(
            "## Chama Viva\n\n**Gasto:** 2 PI por Dado.\n\nMax. 3 Dado por Grau/Círculo.\n\nEnvolve o alvo em chamas.\n\n" +
            "Obrigatória a compra de Duração.\n\nObrigatória a compra de Dano ou Alcance.\n");
    }

    [Fact]
    public void Max_not_per_grau() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.PorUnidade, porUnidade: 1, unidade: "Alvo", max: 2)).Should().Contain("\n\nMax. 2 Alvo.\n\n");

    [Theory]
    [InlineData(TipoDeCusto.Manual, null, "**Gasto:** X PI.")]
    [InlineData(TipoDeCusto.ManualPorUnidade, "Dado", "**Gasto:** X PI por Dado.")]
    public void Manual_costs(TipoDeCusto tipo, string? unidade, string esperado) =>
        EfeitoMarkdownBlock.Gerar(Efeito(tipo, unidade: unidade)).Should().Contain(esperado);

    [Fact]
    public void Derived_cost() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.DerivadoDeOutroEfeito, derivado: "Dano"))
            .Should().Contain("**Gasto:** igual à Quantidade de Dano.");

    [Fact]
    public void Alternative_cost() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.Fixo, fixo: 3, alt: 5, altGrau: 4))
            .Should().Contain("**Gasto:** 3 PI (5 PI a partir do 4º Grau).");
}
