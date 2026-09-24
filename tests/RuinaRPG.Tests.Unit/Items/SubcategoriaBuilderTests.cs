using FluentAssertions;
using RuinaRPG.Domain.Items;

namespace RuinaRPG.Tests.Unit.Items;

public class SubcategoriaBuilderTests
{
    [Fact]
    public void Compose_joins_exactly_4_segments_with_no_grammar_transformation()
    {
        SubcategoriaBuilder.Compose(ItemTipo.Arma, "Mágica", "Varinha").Should().Be("Equipamento inicial - Arma - Mágica - Varinha");
        SubcategoriaBuilder.Compose(ItemTipo.Arma, "Distância", "Arcos").Should().Be("Equipamento inicial - Arma - Distância - Arcos");
    }

    [Fact]
    public void TryParse_round_trips_a_composed_string()
    {
        var composed = SubcategoriaBuilder.Compose(ItemTipo.Armadura, "Leve", "Couro");

        var ok = SubcategoriaBuilder.TryParse(composed, out var tipo, out var categoria, out var familia);

        ok.Should().BeTrue();
        tipo.Should().Be(ItemTipo.Armadura);
        categoria.Should().Be("Leve");
        familia.Should().Be("Couro");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Arcos")]
    [InlineData("Equipamento inicial - Arma - Só 3 partes")]
    [InlineData("Prefixo errado - Arma - Distância - Arcos")]
    [InlineData("Equipamento inicial - TipoInexistente - Distância - Arcos")]
    public void TryParse_returns_false_for_anything_that_does_not_match_the_pattern(string? input)
    {
        var ok = SubcategoriaBuilder.TryParse(input, out _, out _, out _);

        ok.Should().BeFalse();
    }
}
