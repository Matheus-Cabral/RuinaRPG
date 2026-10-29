using FluentAssertions;
using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Tests.Unit.Auth;

public class ContaDeJogadorTests
{
    [Fact]
    public void PodeGerir_so_o_GM_que_convidou()
    {
        var gm = Guid.NewGuid();
        ContaDeJogador.PodeGerir(gm, gm).Should().BeTrue();
        ContaDeJogador.PodeGerir(Guid.NewGuid(), gm).Should().BeFalse();
        ContaDeJogador.PodeGerir(gm, null).Should().BeFalse();
    }

    [Fact]
    public void Transicoes_validas()
    {
        ContaDeJogador.Aplicar(EstadoDaConta.Ativa, AcaoDeConta.Desativar).Should().Be(EstadoDaConta.Desativada);
        ContaDeJogador.Aplicar(EstadoDaConta.Desativada, AcaoDeConta.Reativar).Should().Be(EstadoDaConta.Ativa);
        ContaDeJogador.Aplicar(EstadoDaConta.Ativa, AcaoDeConta.Excluir).Should().Be(EstadoDaConta.Excluida);
        ContaDeJogador.Aplicar(EstadoDaConta.Desativada, AcaoDeConta.Excluir).Should().Be(EstadoDaConta.Excluida);
    }

    [Fact]
    public void Transicoes_invalidas_devolvem_null()
    {
        ContaDeJogador.Aplicar(EstadoDaConta.Ativa, AcaoDeConta.Reativar).Should().BeNull();
        ContaDeJogador.Aplicar(EstadoDaConta.Desativada, AcaoDeConta.Desativar).Should().BeNull();
        foreach (var acao in Enum.GetValues<AcaoDeConta>())
            ContaDeJogador.Aplicar(EstadoDaConta.Excluida, acao).Should().BeNull();
    }
}
