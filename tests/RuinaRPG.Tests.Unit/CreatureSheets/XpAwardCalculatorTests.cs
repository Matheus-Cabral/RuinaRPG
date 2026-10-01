using FluentAssertions;
using RuinaRPG.Domain.CreatureSheets;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class XpAwardCalculatorTests
{
    [Fact]
    public void Abate_is_15_percent_of_experiencia_atual_rounded_down()
    {
        // "Abate = piso(Experiência atual × 0,15)" — Ficha de Criaturas R0004.
        XpAwardCalculator.Abate(experienciaAtual: 100).Should().Be(15);
        XpAwardCalculator.Abate(experienciaAtual: 97).Should().Be(14); // 14.55 -> 14
    }

    [Fact]
    public void Assistencia_is_12_percent_of_experiencia_atual_rounded_down()
    {
        // "Assistência = piso(Experiência atual × 0,12)" — Ficha de Criaturas R0004.
        XpAwardCalculator.Assistencia(experienciaAtual: 100).Should().Be(12);
        XpAwardCalculator.Assistencia(experienciaAtual: 97).Should().Be(11); // 11.64 -> 11
    }
}
