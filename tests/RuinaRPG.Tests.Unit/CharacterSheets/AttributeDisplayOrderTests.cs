using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class AttributeDisplayOrderTests
{
    [Fact]
    public void Rank_sorts_the_8_attributes_into_the_requested_display_order()
    {
        var atributos = new[]
        {
            Atributo.Instinto, Atributo.Vontade, Atributo.Vigor, Atributo.Influencia,
            Atributo.Agilidade, Atributo.Destreza, Atributo.Astucia, Atributo.Forca,
        };

        var sorted = atributos.OrderBy(AttributeDisplayOrder.Rank).ToArray();

        sorted.Should().Equal(
            Atributo.Forca, Atributo.Vigor, Atributo.Agilidade, Atributo.Destreza,
            Atributo.Astucia, Atributo.Instinto, Atributo.Influencia, Atributo.Vontade);
    }
}
