using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class LinhagemVarianteValidatorTests
{
    [Theory]
    [InlineData(Linhagem.Humano, Variante.Sinir, true)]
    [InlineData(Linhagem.Humano, Variante.Laonir, true)]
    [InlineData(Linhagem.Humano, Variante.Yavos, false)]
    [InlineData(Linhagem.Phylauc, Variante.PhylacTai, true)]
    [InlineData(Linhagem.Phylauc, Variante.EsPhylauc, true)]
    [InlineData(Linhagem.Nephrytes, Variante.Yavos, true)]
    [InlineData(Linhagem.Nephrytes, Variante.Koroanos, true)]
    [InlineData(Linhagem.Econos, Variante.Alora, true)]
    [InlineData(Linhagem.Econos, Variante.Sinir, false)]
    public void IsValidCombination_matches_the_Sistema_Basico_linhagem_variante_pairing(Linhagem linhagem, Variante variante, bool expected)
    {
        var result = LinhagemVarianteValidator.IsValidCombination(linhagem, variante);

        result.Should().Be(expected);
    }
}
