using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PontosDeIgnicaoCalculatorTests
{
    // Real values from the Tabela de Níveis.
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeAtributo, 9), (ChavesDeNivel.PontosDeIgnicao, 10)),
        Nivel(2, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.PontosDeAtributo, 1)),
        Nivel(11, (ChavesDeNivel.PontosDeIgnicao, 8)));

    [Fact]
    public void ComputeTotal_at_level_1_with_no_manual_bonus_is_just_the_level_1_grant()
    {
        PontosDeIgnicaoCalculator.ComputeTotal(nivel: 1, bonusManual: 0, Niveis).Should().Be(10);
    }

    [Fact]
    public void ComputeTotal_sums_every_level_up_to_and_including_the_current_one()
    {
        PontosDeIgnicaoCalculator.ComputeTotal(nivel: 2, bonusManual: 0, Niveis).Should().Be(14); // 10 + 4
    }

    [Fact]
    public void ComputeTotal_keeps_summing_across_later_levels()
    {
        PontosDeIgnicaoCalculator.ComputeTotal(nivel: 11, bonusManual: 0, Niveis).Should().Be(22); // 10 + 4 + 8
    }

    [Fact]
    public void ComputeTotal_adds_the_manual_bonus_on_top()
    {
        // "O GM também pode conceder PI para os jogadores acrescentarem neste contador" (1.b).
        PontosDeIgnicaoCalculator.ComputeTotal(nivel: 1, bonusManual: 25, Niveis).Should().Be(35); // 10 + 25
    }
}
