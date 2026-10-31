using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class VocacaoArcanaCalculatorTests
{
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
