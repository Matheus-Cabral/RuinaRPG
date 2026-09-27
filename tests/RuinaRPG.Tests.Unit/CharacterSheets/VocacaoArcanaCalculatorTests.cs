using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Rules.ReferenceData;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class VocacaoArcanaCalculatorTests
{
    // Real excerpt from Tabela de Circulo e Grau por EAP: coluna Afinidade (Valores Absolutos) 3/5/7.
    private static readonly IReadOnlyList<CirculoGrauPorEap> Tabela =
    [
        new(0, "0", "0", 3, 0),
        new(1, "100", "100", 5, 2),
        new(2, "400", "300", 7, 2)
    ];

    [Fact]
    public void Gasto_of_no_rows_is_zero()
    {
        VocacaoArcanaCalculator.Gasto([]).Should().Be(0);
    }

    [Fact]
    public void Gasto_sums_each_essencia_value_and_the_sub_elemento_value()
    {
        var linhas = new[] { new LinhaDeAfinidade(Elemento.Fogo, 2, SubElemento.Ferro, 1, EssenciaBasica.Terra, 1) };

        VocacaoArcanaCalculator.Gasto(linhas).Should().Be(4);
    }

    [Fact]
    public void Gasto_counts_a_repeated_essencia_only_once_at_its_highest_value_regardless_of_position()
    {
        var linhas = new[]
        {
            new LinhaDeAfinidade(Elemento.Fogo, 2, SubElemento.Ferro, 1, EssenciaBasica.Terra, 1),
            new LinhaDeAfinidade(Elemento.Ar, 1, SubElemento.Raio, 2, EssenciaBasica.Fogo, 1),
            new LinhaDeAfinidade(Elemento.Terra, 3, null, null, null, null)
        };

        // Fogo max(2,1)=2 + Terra max(1,3)=3 + Ar 1 + Sub-Elementos 1+2 = 9
        VocacaoArcanaCalculator.Gasto(linhas).Should().Be(9);
    }

    [Fact]
    public void Gasto_counts_caminhos_in_the_segunda_essencia()
    {
        var linhas = new[] { new LinhaDeAfinidade(Elemento.Ar, 1, SubElemento.Prever, 0, EssenciaBasica.Alma, 2) };

        VocacaoArcanaCalculator.Gasto(linhas).Should().Be(3);
    }

    [Fact]
    public void Gasto_treats_null_values_as_zero()
    {
        var linhas = new[] { new LinhaDeAfinidade(Elemento.Agua, null, null, null, EssenciaBasica.Terra, null) };

        VocacaoArcanaCalculator.Gasto(linhas).Should().Be(0);
    }

    [Theory]
    [InlineData(Vocacao.Feiticeiro, 0, 3)]
    [InlineData(Vocacao.Adepto, 1, 5)]
    [InlineData(Vocacao.Bruxo, 2, 7)]
    public void Maxima_for_a_magic_vocacao_follows_the_table_for_the_current_circulo(Vocacao vocacao, int circulo, int esperado)
    {
        VocacaoArcanaCalculator.Maxima(vocacao, circulo, afinidadeAdicional: 0, Tabela).Should().Be(esperado);
    }

    [Fact]
    public void Maxima_adds_the_afinidade_adicional()
    {
        VocacaoArcanaCalculator.Maxima(Vocacao.Feiticeiro, 1, afinidadeAdicional: 2, Tabela).Should().Be(7);
    }

    [Theory]
    [InlineData(Vocacao.Campeao)]
    [InlineData(Vocacao.Cacador)]
    [InlineData(null)]
    public void Maxima_for_a_martial_or_missing_vocacao_is_only_the_afinidade_adicional(Vocacao? vocacao)
    {
        VocacaoArcanaCalculator.Maxima(vocacao, 2, afinidadeAdicional: 4, Tabela).Should().Be(4);
    }

    [Theory]
    [InlineData(Vocacao.Feiticeiro, true)]
    [InlineData(Vocacao.Adepto, true)]
    [InlineData(Vocacao.Bruxo, true)]
    [InlineData(Vocacao.Campeao, false)]
    [InlineData(Vocacao.Cacador, false)]
    [InlineData(null, false)]
    public void EhMagica_is_true_only_for_the_three_magic_vocacoes(Vocacao? vocacao, bool esperado)
    {
        VocacaoArcanaCalculator.EhMagica(vocacao).Should().Be(esperado);
    }
}
