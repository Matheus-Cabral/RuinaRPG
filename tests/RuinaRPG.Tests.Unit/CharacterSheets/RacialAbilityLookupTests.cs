using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class RacialAbilityLookupTests
{
    // Real text from Ruína RPG - Sistema Básico.md §7.
    [Theory]
    [InlineData(Variante.Sinir, "Racial (Arca)")]
    [InlineData(Variante.Laonir, "Racial (Arca)")]
    [InlineData(Variante.PhylacTai, "Racial (Lei da Selva)")]
    [InlineData(Variante.EsPhylauc, "Racial (Lei da Selva)")]
    [InlineData(Variante.Yavos, "Racial (Sobre Voo)")]
    [InlineData(Variante.Koroanos, "Racial (Sobre Voo)")]
    [InlineData(Variante.Alora, "Racial (Amplificador Místico)")]
    public void For_returns_the_correct_racial_name_per_variante(Variante variante, string expectedNome)
    {
        var result = RacialAbilityLookup.For(variante);

        result.Nome.Should().Be(expectedNome);
    }

    [Fact]
    public void For_Alora_returns_the_amplificador_mistico_description()
    {
        var result = RacialAbilityLookup.For(Variante.Alora);

        result.Descricao.Should().Contain("aumentar a escala do dado da rolagem em +1 degrau");
    }
}
