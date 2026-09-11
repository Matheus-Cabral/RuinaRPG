namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>GM-facing (Habilidades Raciais page): the current Gratuita/Obrigatória option lists for one Variante, plus whether they're still the hardcoded default or a saved GM override.</summary>
public record RacialTraitSlotsEntryResponse(string Variante, List<RacialTraitOptionResponse> Gratuita, List<RacialTraitOptionResponse> Obrigatoria, bool IsDefault);
