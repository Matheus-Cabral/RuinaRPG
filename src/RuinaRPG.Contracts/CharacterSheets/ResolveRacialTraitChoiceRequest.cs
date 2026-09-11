namespace RuinaRPG.Contracts.CharacterSheets;

/// <summary>
/// ObrigatoriaTraitNome is ignored when the Variante's Obrigatória slot has 0 or 1 options (nothing
/// to choose — 0 means no such slot, 1 means it's auto-granted) and required when it has 2+.
/// </summary>
public record ResolveRacialTraitChoiceRequest(string GratuitaTraitNome, string? ObrigatoriaTraitNome);
