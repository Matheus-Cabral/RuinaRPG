using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class NivelCalculatorTests
{
    // Real excerpt from Tabelas de XP, Atributos, Características e EAP (Nível 1-3, plus the
    // Nível 50 "Lvl. Max" sentinel row that has no real numeric threshold).
    private static readonly IReadOnlyList<XpPorNivel> Tabela =
    [
        new(1, "50", "50"),
        new(2, "150", "100"),
        new(3, "300", "150"),
        new(50, "Lvl. Max", "Lvl. Max")
    ];

    [Fact]
    public void Compute_with_zero_xp_is_Nivel_1()
    {
        NivelCalculator.Compute(0, Tabela).Should().Be(1);
    }

    [Fact]
    public void Compute_one_xp_below_a_levels_threshold_stays_at_the_previous_level()
    {
        NivelCalculator.Compute(49, Tabela).Should().Be(1);
    }

    [Fact]
    public void Compute_exactly_at_a_levels_threshold_already_advances_to_that_level()
    {
        // 50 is Nível 2's threshold — reaching it exactly already counts as Nível 2, not Nível 1.
        NivelCalculator.Compute(50, Tabela).Should().Be(2);
    }

    [Fact]
    public void Compute_past_a_levels_threshold_stays_at_the_level_it_reached()
    {
        NivelCalculator.Compute(51, Tabela).Should().Be(2);
    }

    [Fact]
    public void Compute_at_a_later_levels_threshold_skips_intermediate_levels_correctly()
    {
        // 150 is Nível 2's threshold — reaching it exactly is already Nível 3.
        NivelCalculator.Compute(150, Tabela).Should().Be(3);
    }

    [Fact]
    public void Compute_past_every_numeric_threshold_advances_one_past_the_highest_one_met()
    {
        // This fixture only has numeric rows for Nível 1-3 (then jumps to the Nível 50 sentinel),
        // so exceeding every numeric threshold lands one past the highest numeric row (Nível 4) —
        // the real, contiguous 1-49 table is what actually caps this at Nível 50 in production.
        NivelCalculator.Compute(1_000_000, Tabela).Should().Be(4);
    }

    [Fact]
    public void XpParaProximoNivel_returns_the_remaining_xp_to_the_next_threshold()
    {
        NivelCalculator.XpParaProximoNivel(30, Tabela).Should().Be(20); // Nível 1, needs 20 more to hit 50
    }

    [Fact]
    public void XpParaProximoNivel_at_the_max_level_is_null()
    {
        NivelCalculator.XpParaProximoNivel(1_000_000, Tabela).Should().BeNull();
    }
}
