using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class GraduacaoCalculatorTests
{
    // Real excerpt from Tabela de Circulo e Grau por EAP: 0→0, 1→100, 2→400 EAP absolute thresholds.
    private static readonly IReadOnlyList<CirculoGrauPorEap> Tabela =
    [
        new(0, "0", "0", 3, 0),
        new(1, "100", "100", 5, 2),
        new(2, "400", "300", 7, 2)
    ];

    [Fact]
    public void Compute_for_Campeao_ignores_coracao_de_mana_and_uses_EAP_directly()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Campeao, eapAtual: 150, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(1); // 150 >= 100 (Grau 1's threshold), < 400 (Grau 2's threshold)
    }

    [Fact]
    public void Compute_for_Cacador_ignores_coracao_de_mana_too()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Cacador, eapAtual: 450, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(2);
    }

    [Fact]
    public void Compute_for_a_magic_vocacao_without_coracao_de_mana_is_always_zero()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Feiticeiro, eapAtual: 450, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(0);
    }

    [Fact]
    public void Compute_for_a_magic_vocacao_with_coracao_de_mana_uses_EAP_normally()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Feiticeiro, eapAtual: 450, possuiCoracaoDeMana: true, Tabela);

        result.Should().Be(2);
    }

    [Fact]
    public void Compute_below_the_first_threshold_is_zero()
    {
        var result = GraduacaoCalculator.Compute(Vocacao.Campeao, eapAtual: 50, possuiCoracaoDeMana: false, Tabela);

        result.Should().Be(0);
    }
}
