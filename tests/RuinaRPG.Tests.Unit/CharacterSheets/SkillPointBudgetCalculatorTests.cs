using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class SkillPointBudgetCalculatorTests
{
    // Real values from the Tabela de Níveis.
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeAtributo, 9), (ChavesDeNivel.PontosDePericia, 4)),
        Nivel(10, (ChavesDeNivel.PontosDeIgnicao, 8), (ChavesDeNivel.PontosDePericia, 2)),
        Nivel(12, (ChavesDeNivel.PontosDeIgnicao, 8), (ChavesDeNivel.PontosDePericia, 1)),
        Nivel(15, (ChavesDeNivel.PontosDeIgnicao, 12), (ChavesDeNivel.PontosDePericia, 2)));

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
    public void Compute_keeps_summing_across_later_levels()
    {
        SkillPointBudgetCalculator.Compute(nivel: 12, Niveis).Should().Be(7); // 4 + 2 + 1
    }

    [Fact]
    public void Compute_ignores_levels_above_the_current_one()
    {
        SkillPointBudgetCalculator.Compute(nivel: 1, Niveis).Should().NotBe(SkillPointBudgetCalculator.Compute(nivel: 15, Niveis));
    }
}
