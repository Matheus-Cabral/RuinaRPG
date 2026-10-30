using FluentAssertions;
using RuinaRPG.Domain.Auth;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CemiterioTests
{
    [Fact]
    public void Sem_motivo_a_lista_e_vazia()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: true, estadoDoDono: EstadoDaConta.Ativa).Should().BeEmpty();
    }

    [Fact]
    public void Morto_da_Morte()
    {
        Cemiterio.Motivos(morto: true, donoEhMembro: true, estadoDoDono: EstadoDaConta.Ativa).Should().Equal(MotivoDoCemiterio.Morte);
    }

    [Fact]
    public void Dono_fora_da_campanha_da_RemovidoDaCampanha()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: false, estadoDoDono: EstadoDaConta.Ativa).Should().Equal(MotivoDoCemiterio.RemovidoDaCampanha);
    }

    [Fact]
    public void Os_dois_motivos_vem_na_ordem_fixa()
    {
        Cemiterio.Motivos(morto: true, donoEhMembro: false, estadoDoDono: EstadoDaConta.Ativa)
            .Should().Equal(MotivoDoCemiterio.Morte, MotivoDoCemiterio.RemovidoDaCampanha);
    }

    [Fact]
    public void Conta_desativada_da_motivo()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: true, estadoDoDono: EstadoDaConta.Desativada)
            .Should().Equal(MotivoDoCemiterio.ContaDesativada);
    }

    [Fact]
    public void Conta_excluida_da_motivo()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: true, estadoDoDono: EstadoDaConta.Excluida)
            .Should().Equal(MotivoDoCemiterio.ContaExcluida);
    }

    [Fact]
    public void Ordem_fixa_com_todos_os_motivos()
    {
        Cemiterio.Motivos(morto: true, donoEhMembro: false, estadoDoDono: EstadoDaConta.Desativada)
            .Should().Equal(MotivoDoCemiterio.Morte, MotivoDoCemiterio.RemovidoDaCampanha, MotivoDoCemiterio.ContaDesativada);
    }
}
