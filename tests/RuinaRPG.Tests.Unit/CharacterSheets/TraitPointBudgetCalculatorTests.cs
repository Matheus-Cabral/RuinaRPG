using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class TraitPointBudgetCalculatorTests
{
    // Real values from the Tabela de Níveis.
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeAtributo, 9)),
        Nivel(4, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.EspacosDeCaracteristica, 1)),
        Nivel(8, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.EspacosDeCaracteristica, 1)));

    [Fact]
    public void Compute_below_the_first_table_grant_is_just_the_creation_base_of_5()
    {
        // 5 is a flat creation grant that never appears in the Tabela de Níveis (unlike
        // Atributo/Perícia, whose level-1 row IS their creation grant) — confirmed by reading the
        // real table: its first "Espaço de Característica" grant is at level 4, nothing at level
        // 1, so this base has to be added here instead.
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

    // Ficha de Personagem 5.d: o limite de Negativas é o dobro do de Positivas.
    [Theory]
    [InlineData(1, 10)]
    [InlineData(4, 12)]
    [InlineData(8, 14)]
    public void ComputeNegativas_is_twice_the_positive_budget_at_every_level(int nivel, int esperado)
    {
        TraitPointBudgetCalculator.ComputeNegativas(nivel, Niveis).Should().Be(esperado);
    }

    [Fact]
    public void Para_picks_the_budget_of_the_polaridade()
    {
        TraitPointBudgetCalculator.Para(RuinaRPG.Domain.Enums.Polaridade.Positiva, nivel: 1, Niveis).Should().Be(5);
        TraitPointBudgetCalculator.Para(RuinaRPG.Domain.Enums.Polaridade.Negativa, nivel: 1, Niveis).Should().Be(10);
    }
}
