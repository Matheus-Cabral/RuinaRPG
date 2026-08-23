using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Domain.CreatureSheets;

public static class CreatureSkillAllowList
{
    private static readonly HashSet<Pericia> Allowed =
    [
        Pericia.Acrobacia, Pericia.ArtefatosMagicos, Pericia.Atletismo, Pericia.Brigar,
        Pericia.EmpatiaComAnimais, Pericia.Enganacao, Pericia.ForcaDeVontade, Pericia.Fortitude,
        Pericia.Furtividade, Pericia.Intimidacao, Pericia.Intuicao, Pericia.Investigacao,
        Pericia.Navegacao, Pericia.Ocultismo, Pericia.Percepcao, Pericia.Pontaria,
        Pericia.Prontidao, Pericia.Reflexos, Pericia.Seducao, Pericia.Sobrevivencia
    ];

    public static bool IsAllowed(Pericia pericia) => Allowed.Contains(pericia);
}
