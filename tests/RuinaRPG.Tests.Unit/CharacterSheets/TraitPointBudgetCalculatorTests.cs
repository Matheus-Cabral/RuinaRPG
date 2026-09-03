using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class TraitPointBudgetCalculatorTests
{
    // Real excerpts from Tabela de Níveis' Bônus column.
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo  <br>+Status de Vida Aprimorado"),
        new(4, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Espaço de Caracteristica"),
        new(8, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Espaço de Caracteristica"),
    ];

    [Fact]
    public void Compute_below_the_first_grant_is_zero()
    {
        TraitPointBudgetCalculator.Compute(nivel: 3, Niveis).Should().Be(0);
    }

    [Fact]
    public void Compute_at_the_first_grant_is_one()
    {
        TraitPointBudgetCalculator.Compute(nivel: 4, Niveis).Should().Be(1);
    }

    [Fact]
    public void Compute_sums_every_grant_up_to_and_including_the_current_level()
    {
        TraitPointBudgetCalculator.Compute(nivel: 8, Niveis).Should().Be(2);
    }
}
