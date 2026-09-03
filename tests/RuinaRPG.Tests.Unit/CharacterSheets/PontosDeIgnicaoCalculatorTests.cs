using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class PontosDeIgnicaoCalculatorTests
{
    // Real excerpts from Tabela de Níveis' Bônus column, including Nível 11's lowercase "pontos".
    private static readonly IReadOnlyList<LevelBonus> Niveis =
    [
        new(1, "+9 Pontos de Atributo  <br>+10 Pontos de Ignição"),
        new(2, "+4 Pontos de Ignição  <br>+1 Ponto de Atributo"),
        new(11, "+8 pontos de Ignição  <br>+Status de Vocação de Vida/Foco"), // lowercase "pontos"
    ];

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
    public void ComputeTotal_counts_a_grant_even_when_pontos_is_spelled_lowercase()
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
