using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class AttributePointBudgetCalculatorTests
{
    // Real excerpts from Tabela de Níveis' Bônus column.
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+10 Pontos de Ignição"),
        new(2, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Ponto de Atributo"),
        new(3, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco"),
        new(40, "+20 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+6 Pontos de Atributo  <br>+1 Espaço de Maestria"),
    ];

    [Fact]
    public void Compute_at_level_1_is_just_the_level_1_creation_grant()
    {
        // Level 1's own row grants the 9 "pontos de distribuição inicial da criação" — not a
        // separate flat add-on on top of it.
        AttributePointBudgetCalculator.Compute(nivel: 1, Niveis).Should().Be(9);
    }

    [Fact]
    public void Compute_adds_every_level_up_to_and_including_the_current_one()
    {
        // 9 (level 1) + 1 (level 2) — level 3 grants no attribute points at all.
        AttributePointBudgetCalculator.Compute(nivel: 3, Niveis).Should().Be(10);
    }

    [Fact]
    public void Compute_ignores_levels_above_the_current_one()
    {
        AttributePointBudgetCalculator.Compute(nivel: 3, Niveis).Should().NotBe(AttributePointBudgetCalculator.Compute(nivel: 40, Niveis));
    }

    [Fact]
    public void Compute_handles_a_larger_multi_digit_grant()
    {
        // 9 (level 1) + 1 (level 2) + 6 (level 40) = 16.
        AttributePointBudgetCalculator.Compute(nivel: 40, Niveis).Should().Be(16);
    }
}
