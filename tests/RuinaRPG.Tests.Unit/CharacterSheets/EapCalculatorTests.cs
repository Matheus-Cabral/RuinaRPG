using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class EapCalculatorTests
{
    private static readonly IReadOnlyList<EapPorNivel> Tabela = [new(1, 0), new(2, 30), new(3, 60)];

    [Fact]
    public void Compute_adds_the_level_base_and_the_Ambares_by_Rank()
    {
        // Nível 2 base = 30. Rank F=5, D=40 (2 Âmbares Rank F, 1 Âmbar Rank D) → 30 + 2*5 + 40 = 80.
        var result = EapCalculator.Compute(nivel: 2, nucleosRankF: 2, nucleosRankE: 0, nucleosRankD: 1,
            nucleosRankC: 0, nucleosRankB: 0, nucleosRankA: 0, nucleosRankS: 0, Tabela);

        result.Should().Be(80);
    }

    [Fact]
    public void Compute_with_no_Ambares_is_just_the_level_base()
    {
        var result = EapCalculator.Compute(nivel: 3, nucleosRankF: 0, nucleosRankE: 0, nucleosRankD: 0,
            nucleosRankC: 0, nucleosRankB: 0, nucleosRankA: 0, nucleosRankS: 0, Tabela);

        result.Should().Be(60);
    }

    [Fact]
    public void Compute_for_a_level_not_in_the_table_treats_the_base_as_zero()
    {
        var result = EapCalculator.Compute(nivel: 999, nucleosRankF: 1, nucleosRankE: 0, nucleosRankD: 0,
            nucleosRankC: 0, nucleosRankB: 0, nucleosRankA: 0, nucleosRankS: 0, Tabela);

        result.Should().Be(5); // just the 1 Rank F Âmbar
    }

    [Fact]
    public void Compute_sums_every_Rank_at_its_own_value()
    {
        // F=5, E=15, D=40, C=120, B=350, A=1000, S=3000, one of each, no level base (Nível 1 → 0).
        var result = EapCalculator.Compute(nivel: 1, nucleosRankF: 1, nucleosRankE: 1, nucleosRankD: 1,
            nucleosRankC: 1, nucleosRankB: 1, nucleosRankA: 1, nucleosRankS: 1, Tabela);

        result.Should().Be(5 + 15 + 40 + 120 + 350 + 1000 + 3000);
    }
}
