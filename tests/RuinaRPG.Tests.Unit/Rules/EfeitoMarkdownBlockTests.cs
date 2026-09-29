using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.Rules;

namespace RuinaRPG.Tests.Unit.Rules;

public class EfeitoMarkdownBlockTests
{
    private static EfeitoParaLivro Efeito(TipoDeCusto tipo, int? fixo = null, int? porUnidade = null, string? unidade = null,
        string? derivado = null, string nome = "Chama Viva", string descricao = "Envolve o alvo em chamas.", int? max = null, bool maxPorGrau = false, int? maxAPartir = null, int? alt = null, int? altGrau = null,
        IReadOnlyList<IReadOnlyList<string>>? pre = null) =>
        new(nome, 2, descricao, tipo, fixo, porUnidade, unidade, derivado, max, maxPorGrau, maxAPartir, alt, altGrau, pre ?? []);

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

    [Fact]
    public void Max_counting_from_a_grau() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.PorUnidade, porUnidade: 3, unidade: "Dado", max: 3, maxPorGrau: true, maxAPartir: 7))
            .Should().Contain("\n\nMax. 3 Dado por Grau/Círculo, contando a partir do 7º Grau/Círculo.\n\n");

    [Fact]
    public void Lines_starting_with_hash_are_escaped_so_they_cannot_become_headings() =>
        EfeitoMarkdownBlock.Gerar(Efeito(TipoDeCusto.Fixo, fixo: 3, nome: "# Chama", descricao: "Linha um.\n# Falso Grau\n## Falso bloco\nFim #."))
            .Should().Be("## \\# Chama\n\n**Gasto:** 3 PI.\n\nLinha um.\n\\# Falso Grau\n\\## Falso bloco\nFim #.\n");
}
