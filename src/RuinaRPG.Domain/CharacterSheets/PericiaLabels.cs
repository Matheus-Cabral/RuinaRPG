namespace RuinaRPG.Domain.CharacterSheets;

/// <summary>
/// Canonical Portuguese display label for each Pericia enum value (e.g. "Empatia c/ Animais" for
/// EmpatiaComAnimais) — the single source of truth other layers derive from instead of keeping
/// their own copy: RuinaRPG.Client.Shared.PericiaDisplay delegates its string-keyed overload to
/// this, and RuinaRPG.Domain.Rules.HistoricoSeedParser's label-to-enum lookup is this map inverted.
/// </summary>
public static class PericiaLabels
{
    private static readonly Dictionary<Pericia, string> Labels = new()
    {
        [Pericia.Acrobacia] = "Acrobacia",
        [Pericia.Alquimia] = "Alquimia",
        [Pericia.Arcano] = "Arcano",
        [Pericia.Armadilhas] = "Armadilhas",
        [Pericia.ArmasBrancas] = "Armas Brancas",
        [Pericia.ArtefatosMagicos] = "Artefatos Mágicos",
        [Pericia.Artistico] = "Artístico",
        [Pericia.Atletismo] = "Atletismo",
        [Pericia.Avaliacao] = "Avaliação",
        [Pericia.Biblioteca] = "Biblioteca",
        [Pericia.Brigar] = "Brigar",
        [Pericia.Conducao] = "Condução",
        [Pericia.Conhecimentos] = "Conhecimentos",
        [Pericia.Crime] = "Crime",
        [Pericia.EmpatiaComAnimais] = "Empatia c/ Animais",
        [Pericia.Enganacao] = "Enganação",
        [Pericia.ForcaDeVontade] = "Força de Vontade",
        [Pericia.Fortitude] = "Fortitude",
        [Pericia.Furtividade] = "Furtividade",
        [Pericia.Herborismo] = "Herborismo",
        [Pericia.Intimidacao] = "Intimidação",
        [Pericia.Intuicao] = "Intuição",
        [Pericia.Investigacao] = "Investigação",
        [Pericia.Labia] = "Lábia",
        [Pericia.Lideranca] = "Liderança",
        [Pericia.Linguistica] = "Linguística",
        [Pericia.Medicina] = "Medicina",
        [Pericia.Navegacao] = "Navegação",
        [Pericia.Ocultismo] = "Ocultismo",
        [Pericia.Oficio] = "Ofício",
        [Pericia.Percepcao] = "Percepção",
        [Pericia.Pontaria] = "Pontaria",
        [Pericia.Prontidao] = "Prontidão",
        [Pericia.Reflexos] = "Reflexos",
        [Pericia.Religiao] = "Religião",
        [Pericia.Saquear] = "Saquear",
        [Pericia.Seducao] = "Sedução",
        [Pericia.SensoComum] = "Senso Comum",
        [Pericia.Sobrevivencia] = "Sobrevivência",
    };

    /// <summary>Falls back to the enum's own name for anything not in the map — shouldn't happen, the map covers every Pericia value.</summary>
    public static string Label(Pericia pericia) => Labels.GetValueOrDefault(pericia, pericia.ToString());
}
