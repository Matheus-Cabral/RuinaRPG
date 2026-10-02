using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CarryWeightCalculatorTests
{
    [Fact]
    public void PesoMaximo_floors_Forca_plus_Vigor_over_2_and_adds_capacidade_extra()
    {
        // Limite de Carga = piso((Força+Vigor)/2) — Requisitos - Ficha de Personagem 2.b — plus
        // any Capacidade Extra from Inventário "mochila"-type items.
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 0m).Should().Be(4m); // floor(9/2)=4
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 10m).Should().Be(14m);
    }

    [Fact]
    public void PesoMaximo_adds_a_positive_campaign_bonus()
    {
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 10m, bonusDeCarga: 6m).Should().Be(20m);
    }

    [Fact]
    public void PesoMaximo_subtracts_a_negative_campaign_bonus()
    {
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 0m, bonusDeCarga: -3m).Should().Be(1m);
    }

    [Fact]
    public void PesoMaximo_never_goes_below_zero()
    {
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 0m, bonusDeCarga: -100m).Should().Be(0m);
    }

    [Fact]
    public void PesoMaximo_without_a_bonus_keeps_the_previous_result()
    {
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 2m, bonusDeCarga: 0m).Should().Be(6m);
        CarryWeightCalculator.PesoMaximo(forca: 5, vigor: 4, capacidadeExtraTotal: 2m).Should().Be(6m);
    }

    [Fact]
    public void CountsTowardPesoAtual_is_true_when_CapacidadeExtra_is_null()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(null).Should().BeTrue();
    }

    [Fact]
    public void CountsTowardPesoAtual_is_true_when_CapacidadeExtra_is_zero()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(0m).Should().BeTrue();
    }

    [Fact]
    public void CountsTowardPesoAtual_is_false_when_CapacidadeExtra_is_positive()
    {
        CarryWeightCalculator.CountsTowardPesoAtual(5m).Should().BeFalse();
    }
}
