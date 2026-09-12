using FluentAssertions;
using RuinaRPG.Client.Shared;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class AtributoDisplayTests
{
    [Theory]
    [InlineData("Instinto", "Instinto")]
    [InlineData("Vontade", "Vontade")]
    [InlineData("Vigor", "Vigor")]
    [InlineData("Influencia", "Influência")]
    [InlineData("Agilidade", "Agilidade")]
    [InlineData("Destreza", "Destreza")]
    [InlineData("Astucia", "Astúcia")]
    [InlineData("Forca", "Força")]
    public void Label_returns_the_proper_Portuguese_display_name_for_every_Atributo(string raw, string expected)
    {
        AtributoDisplay.Label(raw).Should().Be(expected);
    }

    [Fact]
    public void Label_falls_back_to_the_raw_value_for_an_unmapped_string()
    {
        // Defensive: never blank out or throw on a value this map hasn't caught up with yet.
        AtributoDisplay.Label("AlgoNovo").Should().Be("AlgoNovo");
    }
}
