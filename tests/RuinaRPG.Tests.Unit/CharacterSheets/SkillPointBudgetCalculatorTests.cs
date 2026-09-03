using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SkillPointBudgetCalculatorTests
{
    // Real excerpts from Tabela de Níveis' Bônus column, including its "Pericia" (unaccented) rows.
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo  <br>+4 Pontos de Perícia"),
        new(10, "+8 Pontos de Ignição  <br>+2 Pontos de Perícia"),
        new(12, "+8 Pontos de Ignição  <br>+1 Pontos de Pericia"), // unaccented spelling
        new(15, "+12 Pontos de Ignição  <br>+2 Pontos de Perícia"),
    ];

    [Fact]
    public void Compute_at_level_1_is_just_the_level_1_grant()
    {
        SkillPointBudgetCalculator.Compute(nivel: 1, Niveis).Should().Be(4);
    }

    [Fact]
    public void Compute_sums_every_level_up_to_and_including_the_current_one()
    {
        SkillPointBudgetCalculator.Compute(nivel: 10, Niveis).Should().Be(6); // 4 + 2
    }

    [Fact]
    public void Compute_counts_the_unaccented_Pericia_spelling_too()
    {
        SkillPointBudgetCalculator.Compute(nivel: 12, Niveis).Should().Be(7); // 4 + 2 + 1
    }

    [Fact]
    public void Compute_ignores_levels_above_the_current_one()
    {
        SkillPointBudgetCalculator.Compute(nivel: 1, Niveis).Should().NotBe(SkillPointBudgetCalculator.Compute(nivel: 15, Niveis));
    }
}
