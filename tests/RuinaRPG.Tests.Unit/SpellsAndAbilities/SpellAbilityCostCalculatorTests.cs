using FluentAssertions;
using RuinaRPG.Domain.SpellsAndAbilities;

namespace RuinaRPG.Tests.Unit.SpellsAndAbilities;

public class SpellAbilityCostCalculatorTests
{
    [Fact]
    public void GastoEmPI_sums_every_effects_CustoPI()
    {
        var result = SpellAbilityCostCalculator.GastoEmPI([2, 3, 4]);

        result.Should().Be(9);
    }

    [Fact]
    public void GastoEmPI_is_zero_for_an_entry_with_no_effects_yet()
    {
        var result = SpellAbilityCostCalculator.GastoEmPI([]);

        result.Should().Be(0);
    }

    [Fact]
    public void Custo_rounds_up_1_point_25_times_GastoEmPI()
    {
        // "Custo: 1,25 de arcana por PI (arredondado para cima)" — GRAUS & CÍRCULOS.md.
        SpellAbilityCostCalculator.Custo(gastoEmPI: 8).Should().Be(10);   // 8 * 1.25 = 10.0, exact
        SpellAbilityCostCalculator.Custo(gastoEmPI: 9).Should().Be(12);  // 9 * 1.25 = 11.25, rounds up to 12
        SpellAbilityCostCalculator.Custo(gastoEmPI: 0).Should().Be(0);
    }
}
