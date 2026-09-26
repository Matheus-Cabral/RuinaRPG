using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class MatrizElementalTests
{
    [Theory]
    [InlineData(Elemento.Ar, EssenciaBasica.Agua, SubElemento.Gelo)]
    [InlineData(Elemento.Agua, EssenciaBasica.Ar, SubElemento.Gelo)]
    [InlineData(Elemento.Ar, EssenciaBasica.Fogo, SubElemento.Raio)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Ar, SubElemento.Raio)]
    [InlineData(Elemento.Agua, EssenciaBasica.Terra, SubElemento.Flora)]
    [InlineData(Elemento.Terra, EssenciaBasica.Agua, SubElemento.Flora)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Terra, SubElemento.Ferro)]
    [InlineData(Elemento.Terra, EssenciaBasica.Fogo, SubElemento.Ferro)]
    [InlineData(Elemento.Ar, EssenciaBasica.Alma, SubElemento.Prever)]
    [InlineData(Elemento.Agua, EssenciaBasica.Alma, SubElemento.Purificar)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Vida, SubElemento.Curar)]
    [InlineData(Elemento.Terra, EssenciaBasica.Vida, SubElemento.Aprimorar)]
    [InlineData(Elemento.Ar, EssenciaBasica.Mundano, SubElemento.Ecomancia)]
    [InlineData(Elemento.Agua, EssenciaBasica.Mundano, SubElemento.Hemomancia)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Mundano, SubElemento.Necromancia)]
    [InlineData(Elemento.Terra, EssenciaBasica.Mundano, SubElemento.Invocacao)]
    public void Intersecao_follows_the_Matriz_Elemental(Elemento e1, EssenciaBasica e2, SubElemento esperado)
    {
        MatrizElemental.Intersecao(e1, e2).Should().Be(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, EssenciaBasica.Terra)]
    [InlineData(Elemento.Terra, EssenciaBasica.Ar)]
    [InlineData(Elemento.Agua, EssenciaBasica.Fogo)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Agua)]
    [InlineData(Elemento.Ar, EssenciaBasica.Ar)]
    [InlineData(Elemento.Agua, EssenciaBasica.Agua)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Fogo)]
    [InlineData(Elemento.Terra, EssenciaBasica.Terra)]
    [InlineData(Elemento.Fogo, EssenciaBasica.Alma)]
    [InlineData(Elemento.Terra, EssenciaBasica.Alma)]
    [InlineData(Elemento.Ar, EssenciaBasica.Vida)]
    [InlineData(Elemento.Agua, EssenciaBasica.Vida)]
    public void Intersecao_is_null_when_the_essencias_dont_cross(Elemento e1, EssenciaBasica e2)
    {
        MatrizElemental.Intersecao(e1, e2).Should().BeNull();
    }

    [Theory]
    [InlineData(Elemento.Ar, new[] { EssenciaBasica.Agua, EssenciaBasica.Fogo, EssenciaBasica.Alma, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Agua, new[] { EssenciaBasica.Ar, EssenciaBasica.Terra, EssenciaBasica.Alma, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Fogo, new[] { EssenciaBasica.Ar, EssenciaBasica.Terra, EssenciaBasica.Vida, EssenciaBasica.Mundano })]
    [InlineData(Elemento.Terra, new[] { EssenciaBasica.Agua, EssenciaBasica.Fogo, EssenciaBasica.Vida, EssenciaBasica.Mundano })]
    public void OpcoesSegundaEssencia_lists_exactly_the_crossing_essencias_in_enum_order(Elemento e1, EssenciaBasica[] esperado)
    {
        MatrizElemental.OpcoesSegundaEssencia(e1).Should().Equal(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Gelo, EssenciaBasica.Agua)]
    [InlineData(Elemento.Agua, SubElemento.Gelo, EssenciaBasica.Ar)]
    [InlineData(Elemento.Fogo, SubElemento.Curar, EssenciaBasica.Vida)]
    [InlineData(Elemento.Ar, SubElemento.Ecomancia, EssenciaBasica.Mundano)]
    public void SegundaEssenciaQueProduz_inverts_the_intersection(Elemento e1, SubElemento sub, EssenciaBasica esperado)
    {
        MatrizElemental.SegundaEssenciaQueProduz(e1, sub).Should().Be(esperado);
    }

    [Theory]
    [InlineData(Elemento.Ar, SubElemento.Curar)]
    [InlineData(Elemento.Ar, SubElemento.Alma)]
    [InlineData(Elemento.Fogo, SubElemento.Vida)]
    [InlineData(Elemento.Terra, SubElemento.Gelo)]
    public void SegundaEssenciaQueProduz_is_null_when_the_SubElemento_cant_come_from_that_Elemento(Elemento e1, SubElemento sub)
    {
        MatrizElemental.SegundaEssenciaQueProduz(e1, sub).Should().BeNull();
    }

    [Fact]
    public void EssenciaBasica_integer_values_are_fixed_and_match_Elemento()
    {
        ((int)EssenciaBasica.Ar).Should().Be((int)Elemento.Ar);
        ((int)EssenciaBasica.Agua).Should().Be((int)Elemento.Agua);
        ((int)EssenciaBasica.Fogo).Should().Be((int)Elemento.Fogo);
        ((int)EssenciaBasica.Terra).Should().Be((int)Elemento.Terra);
        ((int)EssenciaBasica.Alma).Should().Be(4);
        ((int)EssenciaBasica.Vida).Should().Be(5);
        ((int)EssenciaBasica.Mundano).Should().Be(6);
    }
}
