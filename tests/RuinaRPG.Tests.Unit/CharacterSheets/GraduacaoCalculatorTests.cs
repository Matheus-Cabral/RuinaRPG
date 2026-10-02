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

    [Theory]
    [InlineData(Vocacao.Campeao, 0, 1)]
    [InlineData(Vocacao.Campeao, 50, 1)]
    [InlineData(Vocacao.Campeao, 99, 1)]
    [InlineData(Vocacao.Campeao, 100, 1)]
    [InlineData(Vocacao.Campeao, 399, 1)]
    [InlineData(Vocacao.Campeao, 400, 2)]
    [InlineData(Vocacao.Cacador, 0, 1)]
    [InlineData(Vocacao.Cacador, 99, 1)]
    [InlineData(Vocacao.Cacador, 100, 1)]
    [InlineData(Vocacao.Cacador, 399, 1)]
    [InlineData(Vocacao.Cacador, 400, 2)]
    public void Compute_for_martial_vocacoes_never_drops_below_Grau_1(Vocacao vocacao, int eap, int esperado)
    {
        GraduacaoCalculator.Compute(vocacao, eap, possuiCoracaoDeMana: false, Tabela).Should().Be(esperado);
    }

    [Theory]
    [InlineData(Vocacao.Campeao)]
    [InlineData(Vocacao.Cacador)]
    public void Compute_for_martial_vocacoes_reaches_Grau_3_at_1100(Vocacao vocacao)
    {
        var tabela = Tabela.Append(new CirculoGrauPorEap(3, "1100", "700", 9, 2)).ToList();

        GraduacaoCalculator.Compute(vocacao, 1100, possuiCoracaoDeMana: false, tabela).Should().Be(3);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(99, 0)]
    [InlineData(100, 1)]
    public void Compute_for_a_magic_vocacao_with_coracao_de_mana_keeps_the_table_thresholds(int eap, int esperado)
    {
        GraduacaoCalculator.Compute(Vocacao.Feiticeiro, eap, possuiCoracaoDeMana: true, Tabela).Should().Be(esperado);
    }

    [Fact]
    public void Compute_for_a_magic_vocacao_without_coracao_de_mana_is_zero_at_eap_zero()
    {
        GraduacaoCalculator.Compute(Vocacao.Feiticeiro, 0, possuiCoracaoDeMana: false, Tabela).Should().Be(0);
    }
}
