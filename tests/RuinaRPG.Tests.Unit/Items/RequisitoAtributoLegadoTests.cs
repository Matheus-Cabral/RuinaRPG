using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class RequisitoAtributoLegadoTests
{
    [Theory]
    [InlineData("10 Dex", Atributo.Destreza, 10)]
    [InlineData("  3 dex ", Atributo.Destreza, 3)]
    [InlineData("Dex 12", Atributo.Destreza, 12)]
    [InlineData("8 Força", Atributo.Forca, 8)]
    [InlineData("8 For", Atributo.Forca, 8)]
    [InlineData("5 Vigor", Atributo.Vigor, 5)]
    [InlineData("6 Agi", Atributo.Agilidade, 6)]
    public void Parses_number_and_attribute_in_either_order(string texto, Atributo atributo, int minimo)
    {
        RequisitoAtributoLegado.TryParse(texto, out var a, out var m).Should().BeTrue();
        a.Should().Be(atributo);
        m.Should().Be(minimo);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Dex 10 ou For 12")]
    [InlineData("10")]
    [InlineData("Dex")]
    [InlineData("10 Sorte")]
    [InlineData("-3 Dex")]
    public void Anything_else_is_not_parsed(string? texto) =>
        RequisitoAtributoLegado.TryParse(texto, out _, out _).Should().BeFalse();

    [Fact]
    public void Every_attribute_has_at_least_its_own_name_as_an_alias() =>
        Enum.GetValues<Atributo>().Should().OnlyContain(a => RequisitoAtributoLegado.Abreviacoes.Values.Contains(a));
}
