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
