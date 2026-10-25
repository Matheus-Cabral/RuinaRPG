using FluentAssertions;
using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Tests.Unit.CharacterSheets;

public class CemiterioTests
{
    [Fact]
    public void Sem_motivo_a_lista_e_vazia()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: true).Should().BeEmpty();
    }

    [Fact]
    public void Morto_da_Morte()
    {
        Cemiterio.Motivos(morto: true, donoEhMembro: true).Should().Equal(MotivoDoCemiterio.Morte);
    }

    [Fact]
    public void Dono_fora_da_campanha_da_RemovidoDaCampanha()
    {
        Cemiterio.Motivos(morto: false, donoEhMembro: false).Should().Equal(MotivoDoCemiterio.RemovidoDaCampanha);
    }

    [Fact]
    public void Os_dois_motivos_vem_na_ordem_fixa()
    {
        Cemiterio.Motivos(morto: true, donoEhMembro: false)
            .Should().Equal(MotivoDoCemiterio.Morte, MotivoDoCemiterio.RemovidoDaCampanha);
    }
}
