using RuinaRPG.Domain.Auth;

namespace RuinaRPG.Domain.CharacterSheets;

// A ficha vai para o cemitério por morte, porque o dono saiu da campanha ou porque a conta
// do dono foi desativada/excluída; os motivos vêm sempre na mesma ordem.
public static class Cemiterio
{
    public static IReadOnlyList<MotivoDoCemiterio> Motivos(bool morto, bool donoEhMembro, EstadoDaConta estadoDoDono)
    {
        var motivos = new List<MotivoDoCemiterio>();
        if (morto) motivos.Add(MotivoDoCemiterio.Morte);
        if (!donoEhMembro) motivos.Add(MotivoDoCemiterio.RemovidoDaCampanha);
        if (estadoDoDono == EstadoDaConta.Desativada) motivos.Add(MotivoDoCemiterio.ContaDesativada);
        if (estadoDoDono == EstadoDaConta.Excluida) motivos.Add(MotivoDoCemiterio.ContaExcluida);
        return motivos;
    }
}
