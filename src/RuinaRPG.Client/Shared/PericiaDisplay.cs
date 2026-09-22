using RuinaRPG.Domain.CharacterSheets;

namespace RuinaRPG.Client.Shared;

/// <summary>
/// Maps a Pericia enum's wire identifier (e.g. "ArmasBrancas", as sent by every skills/mastery
/// endpoint via Pericia.ToString()) to its proper Portuguese display name ("Armas Brancas"), via
/// the canonical RuinaRPG.Domain.CharacterSheets.PericiaLabels map. Display-only: the raw
/// identifier is still what's bound to every dropdown's Value and sent back on save.
/// </summary>
public static class PericiaDisplay
{
    /// <summary>Falls back to the raw value itself for anything not recognized, rather than blanking or throwing.</summary>
    public static string Label(string pericia) =>
        Enum.TryParse<Pericia>(pericia, out var parsed) ? PericiaLabels.Label(parsed) : pericia;
}
