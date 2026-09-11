namespace RuinaRPG.Client.Shared;

/// <summary>
/// Maps a Pericia enum's wire identifier (e.g. "ArmasBrancas", as sent by every skills/mastery
/// endpoint via <c>Pericia.ToString()</c> — see RuinaRPG.Domain.CharacterSheets.Pericia) to its
/// proper Portuguese display name ("Armas Brancas"), per the canonical list in
/// "Requisitos - Ficha de Personagem.md" §2.d. Display-only: the raw identifier is still what's
/// bound to every dropdown's Value and sent back on save, so this never touches routing or
/// persistence — only what a human reads on the Fichas de Personagem/NPC/Criatura pages.
/// </summary>
public static class PericiaDisplay
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Acrobacia"] = "Acrobacia",
        ["Alquimia"] = "Alquimia",
        ["Arcano"] = "Arcano",
        ["Armadilhas"] = "Armadilhas",
        ["ArmasBrancas"] = "Armas Brancas",
        ["ArtefatosMagicos"] = "Artefatos Mágicos",
        ["Artistico"] = "Artístico",
        ["Atletismo"] = "Atletismo",
        ["Avaliacao"] = "Avaliação",
        ["Biblioteca"] = "Biblioteca",
        ["Brigar"] = "Brigar",
        ["Conducao"] = "Condução",
        ["Conhecimentos"] = "Conhecimentos",
        ["Crime"] = "Crime",
        ["EmpatiaComAnimais"] = "Empatia c/ Animais",
        ["Enganacao"] = "Enganação",
        ["ForcaDeVontade"] = "Força de Vontade",
        ["Fortitude"] = "Fortitude",
        ["Furtividade"] = "Furtividade",
        ["Herborismo"] = "Herborismo",
        ["Intimidacao"] = "Intimidação",
        ["Intuicao"] = "Intuição",
        ["Investigacao"] = "Investigação",
        ["Labia"] = "Lábia",
        ["Lideranca"] = "Liderança",
        ["Linguistica"] = "Linguística",
        ["Medicina"] = "Medicina",
        ["Navegacao"] = "Navegação",
        ["Ocultismo"] = "Ocultismo",
        ["Oficio"] = "Ofício",
        ["Percepcao"] = "Percepção",
        ["Pontaria"] = "Pontaria",
        ["Prontidao"] = "Prontidão",
        ["Reflexos"] = "Reflexos",
        ["Religiao"] = "Religião",
        ["Saquear"] = "Saquear",
        ["Seducao"] = "Sedução",
        ["SensoComum"] = "Senso Comum",
        ["Sobrevivencia"] = "Sobrevivência",
    };

    /// <summary>Falls back to the raw value itself for anything not yet in the map, rather than blanking or throwing.</summary>
    public static string Label(string pericia) => Labels.GetValueOrDefault(pericia, pericia);
}
