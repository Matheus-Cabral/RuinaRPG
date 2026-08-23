using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class AttributeTotalCalculatorTests
{
    [Fact]
    public void Total_without_maestria_halves_the_bonus_rounded_down()
    {
        // "Sem Maestria marcada: Total = Gasto + (Bônus / 2) + Artefatos" — Formulas.md.
        var result = AttributeTotalCalculator.Total(gasto: 5, bonus: 3, temMaestria: false, artefatos: 0);

        result.Should().Be(6); // 5 + (3/2 = 1, floor) + 0
    }

    [Fact]
    public void Total_with_maestria_adds_the_full_bonus()
    {
        // "Com Maestria marcada: Total = Gasto + Bônus + Artefatos" — Formulas.md.
        var result = AttributeTotalCalculator.Total(gasto: 5, bonus: 3, temMaestria: true, artefatos: 0);

        result.Should().Be(8);
    }

    [Fact]
    public void Total_adds_artefatos_in_both_cases()
    {
        AttributeTotalCalculator.Total(gasto: 0, bonus: 0, temMaestria: false, artefatos: 4).Should().Be(4);
        AttributeTotalCalculator.Total(gasto: 0, bonus: 0, temMaestria: true, artefatos: 4).Should().Be(4);
    }
}
