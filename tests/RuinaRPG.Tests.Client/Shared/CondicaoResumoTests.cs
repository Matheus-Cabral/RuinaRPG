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

        CondicaoResumo.Linha(linha, -4, k => k).Should().Be("Vigor −4");
    }

    [Fact]
    public void Linha_falls_back_to_the_target_name_for_an_unknown_enum()
    {
        CondicaoResumo.Linha(new("Outro", null, "Fixo", 1, null), 1, k => k).Should().Be("Outro");
    }

    [Theory]
    [InlineData(1, "1 turno")]
    [InlineData(3, "3 turnos")]
    public void Turnos_pluralizes(int n, string esperado) => CondicaoResumo.Turnos(n).Should().Be(esperado);
}
