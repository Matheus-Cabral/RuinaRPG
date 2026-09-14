using FluentAssertions;
using RuinaRPG.Domain.Enums;
using RuinaRPG.Domain.SpellsAndAbilities;
using Xunit;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class EfeitoCustoCalculatorTests
{
    [Fact]
    public void Calcular_Fixo_ignores_quantidade_and_returns_the_flat_cost()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 3, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: null, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(3);
    }

    [Fact]
    public void Calcular_PorUnidade_multiplies_by_Quantidade()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.PorUnidade, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: 4, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(8);
    }

    [Fact]
    public void Calcular_PorUnidade_with_null_Quantidade_treats_it_as_1()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.PorUnidade, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 1, quantidade: null, custoManual: null, quantidadeDerivada: null);

        custo.Should().Be(2);
    }

    [Fact]
    public void Calcular_Manual_returns_the_GM_typed_value_as_is()
    {
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.Manual, custoFixo: null, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 4, quantidade: null, custoManual: 15, quantidadeDerivada: null);

        custo.Should().Be(15);
    }

    [Fact]
    public void Calcular_ManualPorUnidade_multiplies_the_GM_typed_rate_by_Quantidade()
    {
        // Ato Múltiplo: the GM decides the per-Dado rate; Quantidade is how many Dados were bought.
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.ManualPorUnidade, custoFixo: null, custoPorUnidade: null,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 3, quantidade: 2, custoManual: 5, quantidadeDerivada: null);

        custo.Should().Be(10);
    }

    [Fact]
    public void Calcular_DerivadoDeOutroEfeito_multiplies_by_the_derived_Quantidade_not_a_manually_entered_one()
    {
        // Dreno de Vitalidade: 2 PI per Dado, Quantidade comes from the sibling "Dano" effect.
        var custo = EfeitoCustoCalculator.Calcular(TipoDeCusto.DerivadoDeOutroEfeito, custoFixo: null, custoPorUnidade: 2,
            custoAlternativo: null, custoAlternativoAPartirDoGrau: null, grauDaMagia: 3, quantidade: 999 /* ignored */, custoManual: null, quantidadeDerivada: 3);

        custo.Should().Be(6);
    }

    [Fact]
    public void Calcular_applies_CustoAlternativo_once_the_Grau_threshold_is_reached()
    {
        // Encantamento Elemental: 2 PI normally, 4 PI from Grau 4 onward.
        var abaixoDoLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 3, quantidade: null, custoManual: null, quantidadeDerivada: null);
        var noLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 4, quantidade: null, custoManual: null, quantidadeDerivada: null);
        var acimaDoLimiar = EfeitoCustoCalculator.Calcular(TipoDeCusto.Fixo, custoFixo: 2, custoPorUnidade: null,
            custoAlternativo: 4, custoAlternativoAPartirDoGrau: 4, grauDaMagia: 6, quantidade: null, custoManual: null, quantidadeDerivada: null);

        abaixoDoLimiar.Should().Be(2);
        noLimiar.Should().Be(4);
        acimaDoLimiar.Should().Be(4);
    }

    [Fact]
    public void MaxPermitido_returns_null_when_MaxUnidades_is_null()
    {
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: null, maxEscalaPorGrau: false, maxContandoAPartirDoGrau: null, grauDaMagia: 5).Should().BeNull();
    }

    [Fact]
    public void MaxPermitido_returns_the_flat_value_when_it_does_not_scale()
    {
        // Ações por Turno: "Max. 2 ações", no "por Grau/Círculo".
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 2, maxEscalaPorGrau: false, maxContandoAPartirDoGrau: null, grauDaMagia: 8).Should().Be(2);
    }

    [Fact]
    public void MaxPermitido_scales_by_the_raw_Grau_when_no_starting_offset_is_given()
    {
        // Aumentar Armadura: "Max. 5 de Redução por Grau/Círculo".
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 5, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: null, grauDaMagia: 3).Should().Be(15);
    }

    [Fact]
    public void MaxPermitido_scales_relative_to_the_starting_Grau_when_one_is_given()
    {
        // Absorção: "Max. 3 Dados por Grau/Círculo contando a partir do sétimo" — at Grau 7 the
        // multiplier is 1 (3 dados), at Grau 9 it's 3 (9 dados).
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 3, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: 7, grauDaMagia: 7).Should().Be(3);
        EfeitoCustoCalculator.MaxPermitido(maxUnidades: 3, maxEscalaPorGrau: true, maxContandoAPartirDoGrau: 7, grauDaMagia: 9).Should().Be(9);
    }

    [Fact]
    public void DanoAlcanceMaxPorGrau_has_all_9_Graus_matching_the_rulebooks_top_table()
    {
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[1].Should().Be((3, 2));
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[5].Should().Be((7, 6));
        EfeitoCustoCalculator.DanoAlcanceMaxPorGrau[9].Should().Be((11, 10));
    }
}
