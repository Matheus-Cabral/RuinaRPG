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

    /// <summary>
    /// The R0005 "lista fixa mais curta" itself (20 Pericia values), for callers that need to
    /// iterate it — e.g. seeding a CreatureSkill row per allowed Pericia, instead of one per
    /// Enum.GetValues&lt;Pericia&gt;() member like Ficha de NPCs does.
    /// </summary>
    public static IReadOnlyCollection<Pericia> AllowedPericias => Allowed;
}
