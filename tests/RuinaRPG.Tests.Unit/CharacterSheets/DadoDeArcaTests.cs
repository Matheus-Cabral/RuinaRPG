using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;
using Xunit;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class DadoDeArcaTests
{
    [Fact]
    public void Faces_sao_D6_D8_D10_D12_D20_e_D100()
        => DadoDeArca.Faces.Should().Equal(6, 8, 10, 12, 20, 100);

    [Fact]
    public void Padrao_e_D20()
        => DadoDeArca.Padrao.Should().Be(20);

    [Theory]
    [InlineData(6)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(20)]
    [InlineData(100)]
    public void EhValido_aceita_as_faces_permitidas(int faces)
        => DadoDeArca.EhValido(faces).Should().BeTrue();

    [Theory]
    [InlineData(18)]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(-6)]
    [InlineData(101)]
    public void EhValido_rejeita_qualquer_outro_valor(int faces)
        => DadoDeArca.EhValido(faces).Should().BeFalse();
}
