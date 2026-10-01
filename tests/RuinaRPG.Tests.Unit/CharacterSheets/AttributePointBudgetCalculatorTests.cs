using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.Niveis;
using static RuinaRPG.Tests.Unit.Rules.TabelaDeNiveisDeTeste;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class AttributePointBudgetCalculatorTests
{
    // Real values from the Tabela de Níveis.
    private static readonly ProgressaoDeNivel Niveis = Criar(
        Nivel(1, (ChavesDeNivel.PontosDeAtributo, 9), (ChavesDeNivel.PontosDeIgnicao, 10)),
        Nivel(2, (ChavesDeNivel.PontosDeIgnicao, 4), (ChavesDeNivel.PontosDeAtributo, 1)),
        Nivel(3, (ChavesDeNivel.PontosDeIgnicao, 4)),
        Nivel(40, (ChavesDeNivel.PontosDeIgnicao, 20), (ChavesDeNivel.PontosDeAtributo, 6), (ChavesDeNivel.EspacosDeMaestria, 1)));

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

    [Fact]
    public void Compute_reads_only_the_Pontos_de_Atributo_column()
    {
        // The spelling variants of the Markdown ("Pontos de atributo", lowercase) are the
        // extractor's problem now (NivelBonusExtractorTests) — here the column is already a number.
        var soNivel10 = Criar(Nivel(10, (ChavesDeNivel.PontosDeAtributo, 2), (ChavesDeNivel.PontosDePericia, 2), (ChavesDeNivel.PontosDeIgnicao, 8)));

        AttributePointBudgetCalculator.Compute(nivel: 10, soNivel10).Should().Be(2);
    }
}
