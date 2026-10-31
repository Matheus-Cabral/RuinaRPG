using FluentAssertions;
using RuinaRPG.Client.Services;
using RuinaRPG.Contracts.Players;
using RuinaRPG.Domain.Auth;
using Xunit;

namespace RuinaRPG.Tests.Client.Services;

public class EstadoDoJogadorTests
{
    // A API manda o nome do estado (texto); o cliente trabalha com o enum do Domain.
    [Theory]
    [InlineData("Ativa", EstadoDaConta.Ativa)]
    [InlineData("Desativada", EstadoDaConta.Desativada)]
    [InlineData("Excluida", EstadoDaConta.Excluida)]
    public void O_texto_do_estado_vira_o_enum_do_Domain(string texto, EstadoDaConta esperado)
    {
        var jogador = new PlayerSearchResultResponse("1", "nick", "n@x.com", texto);

        jogador.ComoEstadoDaConta().Should().Be(esperado);
    }

    [Fact]
    public void Um_estado_que_o_cliente_nao_conhece_e_um_erro_e_nao_vira_Ativa_em_silencio()
    {
        var jogador = new PlayerSearchResultResponse("1", "nick", "n@x.com", "Inventada");

        var conversao = () => jogador.ComoEstadoDaConta();

        conversao.Should().Throw<ArgumentException>();
    }
}
