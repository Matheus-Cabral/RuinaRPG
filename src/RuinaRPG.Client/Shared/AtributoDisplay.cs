namespace RuinaRPG.Client.Shared;

/// <summary>
/// Maps an Atributo enum's wire identifier (e.g. "Forca", as sent by every attribute/skill/mastery
/// endpoint via <c>Atributo.ToString()</c> — see RuinaRPG.Domain.CharacterSheets.Atributo) to its
/// proper Portuguese display name ("Força"), matching the accent-fix precedent in PericiaDisplay.
/// Display-only: the raw identifier is still what's bound to every dropdown's Value and sent back
/// on save, so this never touches routing or persistence — only what a human reads on the Fichas
/// de Personagem/NPC/Criatura pages.
/// </summary>
public static class AtributoDisplay
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        ["Instinto"] = "Instinto",
        ["Vontade"] = "Vontade",
        ["Vigor"] = "Vigor",
        ["Influencia"] = "Influência",
        ["Agilidade"] = "Agilidade",
        ["Destreza"] = "Destreza",
        ["Astucia"] = "Astúcia",
        ["Forca"] = "Força",
    };

    /// <summary>Falls back to the raw value itself for anything not yet in the map, rather than blanking or throwing.</summary>
    public static string Label(string atributo) => Labels.GetValueOrDefault(atributo, atributo);
}
