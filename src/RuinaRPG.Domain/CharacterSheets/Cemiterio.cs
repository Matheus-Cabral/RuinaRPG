namespace RuinaRPG.Domain.CharacterSheets;

// A ficha vai para o cemitério por morte ou porque o dono saiu da campanha; os motivos
// vêm sempre na mesma ordem (Morte, RemovidoDaCampanha).
public static class Cemiterio
{
    public static IReadOnlyList<MotivoDoCemiterio> Motivos(bool morto, bool donoEhMembro)
    {
        var motivos = new List<MotivoDoCemiterio>();
        if (morto) motivos.Add(MotivoDoCemiterio.Morte);
        if (!donoEhMembro) motivos.Add(MotivoDoCemiterio.RemovidoDaCampanha);
        return motivos;
    }
}
