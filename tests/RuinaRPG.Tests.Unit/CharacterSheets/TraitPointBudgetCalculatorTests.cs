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
    public void Compute_below_the_first_table_grant_is_just_the_creation_base_of_5()
    {
        // 5 is a flat creation grant that never appears as "+X Espaço de Característica" text in
        // Tabela de Níveis (unlike Atributo/Perícia, whose level-1 row IS their creation grant) —
        // confirmed by reading the real table: its first "Espaço de Característica" bonus is at
        // level 4, nothing at level 1, so this base has to be added here instead.
        TraitPointBudgetCalculator.Compute(nivel: 3, Niveis).Should().Be(5);
    }

    [Fact]
    public void Compute_at_the_first_table_grant_is_the_base_plus_one()
    {
        TraitPointBudgetCalculator.Compute(nivel: 4, Niveis).Should().Be(6);
    }

    [Fact]
    public void Compute_sums_every_table_grant_up_to_and_including_the_current_level_on_top_of_the_base()
    {
        TraitPointBudgetCalculator.Compute(nivel: 8, Niveis).Should().Be(7);
    }
}
