using FluentAssertions;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class CreatureAttributePointBudgetCalculatorTests
{
    // Real excerpts from Tabela de Níveis' Bônus column.
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo  <br>+Status de Vida Aprimorado  <br>+10 Pontos de Ignição"),
        new(2, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Ponto de Atributo"),
        new(3, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco"),
        new(6, "+4 Pontos de Ignição  <br>+Status de Vocação de Vida/Foco  <br>+1 Ponto de Atributo"),
    ];

    [Theory]
    [InlineData(Rank.F, 6)]
    [InlineData(Rank.E, 7)]
    [InlineData(Rank.D, 8)]
    [InlineData(Rank.C, 9)]
    [InlineData(Rank.B, 10)]
    [InlineData(Rank.A, 12)]
    [InlineData(Rank.S, 14)]
    public void Compute_at_level_1_is_the_rank_starting_points_instead_of_the_level_1_grant(Rank rank, int esperado)
    {
        CreatureAttributePointBudgetCalculator.Compute(rank, nivel: 1, Niveis).Should().Be(esperado);
    }

    [Fact]
    public void Compute_adds_the_level_table_grants_from_level_2_onward()
    {
        // 9 (Rank C, replaces level 1's +9) + 1 (level 2) + 1 (level 6).
        CreatureAttributePointBudgetCalculator.Compute(Rank.C, nivel: 6, Niveis).Should().Be(11);
        // 14 (Rank S) + 1 (level 2).
        CreatureAttributePointBudgetCalculator.Compute(Rank.S, nivel: 3, Niveis).Should().Be(15);
    }

    [Fact]
    public void Compute_without_a_rank_falls_back_to_the_plain_level_table()
    {
        CreatureAttributePointBudgetCalculator.Compute(rank: null, nivel: 1, Niveis).Should().Be(9);
        CreatureAttributePointBudgetCalculator.Compute(rank: null, nivel: 6, Niveis).Should().Be(11);
    }
}
