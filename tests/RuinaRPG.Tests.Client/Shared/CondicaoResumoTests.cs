using FluentAssertions;
using RuinaRPG.Client.Shared;
using RuinaRPG.Contracts.Condicoes;
using Xunit;

namespace RuinaRPG.Tests.Client.Shared;

public class CondicaoResumoTests
{
    [Fact]
    public void Linha_uses_the_given_value_in_the_domain_text()
    {
        var linha = new CondicaoModificadorDto("Atributo", "Vigor", "Fixo", -2, null);

        CondicaoResumo.Linha(linha, -4, k => k, e => e).Should().Be("Vigor −4");
    }

    [Fact]
    public void Linha_falls_back_to_the_target_name_for_an_unknown_enum()
    {
        CondicaoResumo.Linha(new("Outro", null, "Fixo", 1, null), 1, k => k, e => e).Should().Be("Outro");
    }

    [Fact]
    public void Linha_names_the_element_by_its_catalog_name()
    {
        var linha = new CondicaoModificadorDto("AfinidadeElemental", "19", "Fixo", 2, null);

        CondicaoResumo.Linha(linha, 3, k => k, e => e == "19" ? "Luz" : e).Should().Be("Afinidade de Luz +3");
    }

    [Fact]
    public void LinhasDeDanoElemental_lists_the_non_zero_elements_by_catalog_name_in_id_order()
    {
        var dano = new Dictionary<int, int> { [19] = -1, [2] = 3, [1] = 0 };

        CondicaoResumo.LinhasDeDanoElemental(dano, id => id switch { 2 => "Fogo", 19 => "Luz", _ => "?" })
            .Should().Equal("Dano de Fogo: +3", "Dano de Luz: −1");
    }

    [Theory]
    [InlineData(1, "1 turno")]
    [InlineData(3, "3 turnos")]
    public void Turnos_pluralizes(int n, string esperado) => CondicaoResumo.Turnos(n).Should().Be(esperado);
}
