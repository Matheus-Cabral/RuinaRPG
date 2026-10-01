using FluentAssertions;
using RuinaRPG.Domain.CreatureSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CreatureSheets;

public class CreatureAttributePointBudgetCalculatorTests
{
    // Real values from the Tabela de Níveis.
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeAtributo, 9), (ChavesDeNivel.PontosDeIgnicao, 10)),
        Nivel(2, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.PontosDeAtributo, 1)),
        Nivel(3, (ChavesDeNivel.PontosDeIgnicao, 4)),
        Nivel(6, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.PontosDeAtributo, 1)));

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
