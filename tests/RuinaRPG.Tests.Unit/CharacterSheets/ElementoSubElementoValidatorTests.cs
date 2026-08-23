using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class ElementoSubElementoValidatorTests
{
    // Matriz Elemental, per Ficha de Personagem 2.c:
    // Ar: Gelo, Raio, Prever, Ecomancia, Alma. Água: Gelo, Flora, Purificar, Hemomancia, Alma.
    // Fogo: Raio, Ferro, Curar, Necromancia, Vida. Terra: Ferro, Flora, Aprimorar, Invocação, Vida.
    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Gelo, true)]
    [InlineData(Elemento.Ar, SubElemento.Alma, true)]
    [InlineData(Elemento.Ar, SubElemento.Ferro, false)]
    [InlineData(Elemento.Agua, SubElemento.Flora, true)]
    [InlineData(Elemento.Agua, SubElemento.Alma, true)]
    [InlineData(Elemento.Fogo, SubElemento.Vida, true)]
    [InlineData(Elemento.Terra, SubElemento.Vida, true)]
    [InlineData(Elemento.Terra, SubElemento.Curar, false)]
    public void IsValidCombination_matches_the_Matriz_Elemental(Elemento elemento, SubElemento subElemento, bool expected)
    {
        var result = ElementoSubElementoValidator.IsValidCombination(elemento, subElemento);

        result.Should().Be(expected);
    }
}
